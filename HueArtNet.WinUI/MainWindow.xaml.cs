using HueArtNet.Core.Profiles;
using HueArtNet.Hue.Runtime;
using HueArtNet.Hue.Setup;
using HueArtNet.Persistence;
using HueArtNet.WinUI.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace HueArtNet.WinUI;

public sealed partial class MainWindow : Window
{
  private readonly IServiceScopeFactory scopeFactory;
  private readonly ArtNetShowRuntime showRuntime;
  private readonly IHueBridgeSetupService bridgeSetupService;
  private ShowProfile? currentProfile;
  private HueBridgePairingResult? currentPairingResult;
  private int? currentPairingRowIndex;
  private bool isPairing;
  private bool suppressEntertainmentGroupSelection;
  private bool hasPendingRuntimeChanges;

  public MainWindow(
    IServiceScopeFactory scopeFactory,
    ArtNetShowRuntime showRuntime,
    IHueBridgeSetupService bridgeSetupService)
  {
    this.scopeFactory = scopeFactory;
    this.showRuntime = showRuntime;
    this.bridgeSetupService = bridgeSetupService;
    InitializeComponent();
    ApplyTheme(ElementTheme.Dark);
    Activated += MainWindow_Activated;
  }

  private async void NewTemplate_Click(object sender, RoutedEventArgs e)
  {
    ClearPairingState();
    currentProfile = ProfileEditorMapper.CreateTemplate();
    ApplyProfileToEditor(currentProfile);
    await RefreshStatusAsync("Created a new unsaved 4-hub template.");
  }

  private async void Reload_Click(object sender, RoutedEventArgs e)
  {
    ClearPairingState();
    await LoadEditorAsync("Reloaded stored profile.");
  }

  private async void SaveProfile_Click(object sender, RoutedEventArgs e)
  {
    await SaveProfileFromEditorAsync();
  }

  private async void DiscoverBridges_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      DiscoveredBridgeComboBox.ItemsSource = null;
      ClearPairingState();
      var bridges = await bridgeSetupService.DiscoverAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
      DiscoveredBridgeComboBox.ItemsSource = bridges;
      if (bridges.Count > 0)
        DiscoveredBridgeComboBox.SelectedIndex = 0;

      await RefreshStatusAsync($"Discovery found {bridges.Count} Hue bridge(s).");
    }
    catch (Exception ex)
    {
      await RefreshStatusAsync($"Discovery failed: {ex.Message}");
    }
  }

  private async void PairSelectedBridge_Click(object sender, RoutedEventArgs e)
  {
    if (isPairing)
      return;

    if (DiscoveredBridgeComboBox.SelectedItem is not HueBridgeCandidate bridge)
    {
      await RefreshStatusAsync("Select a discovered Hue bridge before pairing.");
      return;
    }

    try
    {
      isPairing = true;
      PairSelectedBridgeButton.IsEnabled = false;
      ClearPairingState();

      int targetRowIndex = GetSelectedHubRowIndex();
      var result = await bridgeSetupService.PairAsync(
        bridge,
        applicationName: "HueArtNet",
        deviceName: Environment.MachineName,
        CancellationToken.None);

      currentPairingResult = result;
      currentPairingRowIndex = targetRowIndex;
      suppressEntertainmentGroupSelection = true;
      try
      {
        EntertainmentGroupComboBox.ItemsSource = result.EntertainmentGroups;
        if (result.EntertainmentGroups.Count > 0)
          EntertainmentGroupComboBox.SelectedIndex = 0;
      }
      finally
      {
        suppressEntertainmentGroupSelection = false;
      }

      ApplyPairingToHub(result, targetRowIndex, updateCredentials: true);
      var restartMessage = MarkPendingRuntimeChangesIfRunning()
        ? " Restart profile to apply this hub change to the active output."
        : string.Empty;
      await RefreshStatusAsync($"Paired {result.BridgeName}. Select an Entertainment group if more than one was found.{restartMessage}");
    }
    catch (Exception ex)
    {
      await RefreshStatusAsync($"Pairing failed: {ex.Message}");
    }
    finally
    {
      isPairing = false;
      PairSelectedBridgeButton.IsEnabled = true;
    }
  }

  private async void EntertainmentGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (!suppressEntertainmentGroupSelection && currentPairingResult != null && currentPairingRowIndex.HasValue)
    {
      ApplyPairingToHub(currentPairingResult, currentPairingRowIndex.Value, updateCredentials: false);
      if (MarkPendingRuntimeChangesIfRunning())
        await RefreshStatusAsync("Updated paired Entertainment group. Restart profile to apply this change to the active output.");
    }
  }

  private async void StartProfile_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      var profile = BuildProfileFromEditor();
      var validation = ShowProfileValidator.Validate(profile);
      if (!validation.IsValid)
      {
        await RefreshStatusAsync("Profile is invalid. Fix the fields below before starting.", validation.Errors);
        return;
      }

      await WithRepositoryAsync(repository => repository.SaveAsync(profile, CancellationToken.None));
      currentProfile = profile;
      await showRuntime.StartAsync(profile, CancellationToken.None);
      hasPendingRuntimeChanges = false;
      await RefreshStatusAsync($"Started profile '{profile.Name}'.");
    }
    catch (Exception ex)
    {
      await RefreshStatusAsync($"Start failed: {ex.Message}");
    }
  }

  private async void StopProfile_Click(object sender, RoutedEventArgs e)
  {
    await showRuntime.StopAsync();
    hasPendingRuntimeChanges = false;
    await RefreshStatusAsync("Profile stopped.");
  }

  private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
  {
    Activated -= MainWindow_Activated;
    try
    {
      await LoadEditorAsync();
    }
    catch (Exception ex)
    {
      currentProfile = ProfileEditorMapper.CreateTemplate();
      ApplyProfileToEditor(currentProfile);
      await RefreshStatusAsync($"Profile load failed: {ex.Message}");
    }
  }

  private async Task LoadEditorAsync(string? message = null)
  {
    currentProfile = (await WithRepositoryAsync(repository => repository.GetAllAsync(CancellationToken.None))).FirstOrDefault() ?? ProfileEditorMapper.CreateTemplate();
    ApplyProfileToEditor(currentProfile);
    await RefreshStatusAsync(message);
  }

  private async Task SaveProfileFromEditorAsync()
  {
    var profile = BuildProfileFromEditor();
    var validation = ShowProfileValidator.Validate(profile);
    if (!validation.IsValid)
    {
      await RefreshStatusAsync("Profile was not saved because it is invalid.", validation.Errors);
      return;
    }

    await WithRepositoryAsync(repository => repository.SaveAsync(profile, CancellationToken.None));
    currentProfile = profile;
    var restartMessage = MarkPendingRuntimeChangesIfRunning()
      ? " Restart profile to apply saved routing changes to the active output."
      : string.Empty;
    await RefreshStatusAsync($"Saved profile '{profile.Name}'.{restartMessage}");
  }

  private async Task RefreshStatusAsync(string? message = null, IEnumerable<string>? validationErrors = null)
  {
    var profiles = await WithRepositoryAsync(repository => repository.GetAllAsync(CancellationToken.None));
    var runtimeStatus = showRuntime.GetStatus();
    var validationErrorsList = validationErrors?.ToList() ?? new List<string>();
    var editorProfile = currentProfile == null ? null : BuildProfileFromEditor();
    var snapshot = DashboardStatusComposer.Compose(
      profiles,
      runtimeStatus,
      editorProfile,
      validationErrorsList,
      message,
      currentPairingResult?.BridgeName,
      currentPairingRowIndex,
      (DiscoveredBridgeComboBox.SelectedItem as HueBridgeCandidate)?.DisplayName,
      hasPendingRuntimeChanges);

    ApplyDashboardStatus(snapshot);
  }

  private void ApplyDashboardStatus(DashboardStatusSnapshot snapshot)
  {
    RuntimeStateText.Text = snapshot.RuntimeState;
    RuntimeProfileText.Text = snapshot.RuntimeProfile;
    ArtNetStateText.Text = snapshot.ArtNetState;
    ArtNetPacketsText.Text = snapshot.ArtNetPackets;
    HueStateText.Text = snapshot.HueState;
    HueSessionsText.Text = snapshot.HueSessions;
    ValidationStateText.Text = snapshot.ValidationState;
    ValidationDetailsText.Text = snapshot.ValidationDetails;
    BridgeSetupStatusText.Text = snapshot.BridgeSetupStatus;
    StatusText.Text = snapshot.ActivityLog;
  }

  private bool MarkPendingRuntimeChangesIfRunning()
  {
    if (!showRuntime.GetStatus().IsRunning)
      return false;

    hasPendingRuntimeChanges = true;
    return true;
  }

  private async Task WithRepositoryAsync(Func<IShowProfileRepository, Task> action)
  {
    using var scope = scopeFactory.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IShowProfileRepository>();
    await action(repository);
  }

  private async Task<T> WithRepositoryAsync<T>(Func<IShowProfileRepository, Task<T>> action)
  {
    using var scope = scopeFactory.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IShowProfileRepository>();
    return await action(repository);
  }

  private ShowProfile BuildProfileFromEditor()
  {
    return ProfileEditorMapper.BuildProfile(currentProfile, CaptureEditorState());
  }

  private ProfileEditorState CaptureEditorState()
  {
    return new ProfileEditorState(
      Name: ProfileNameTextBox.Text,
      BindAddress: BindAddressTextBox.Text,
      ArtNetPort: ArtNetPortTextBox.Text,
      OutputFps: OutputFpsTextBox.Text,
      TimeoutSeconds: TimeoutSecondsTextBox.Text,
      TimeoutModeTag: GetSelectedTag(TimeoutModeComboBox) ?? string.Empty,
      Hubs: GetHubEditors().Select(CaptureHubState).ToList());
  }

  private static HubEditorState CaptureHubState(HubEditorControls editor)
  {
    return new HubEditorState(
      Enabled: editor.Enabled.IsChecked == true,
      Name: editor.Name.Text,
      BridgeIp: editor.BridgeIp.Text,
      ApplicationKey: editor.ApplicationKey.Password,
      EntertainmentKey: editor.EntertainmentKey.Password,
      EntertainmentGroupId: editor.EntertainmentGroupId.Text,
      Universe: editor.Universe.Text,
      StartChannel: editor.StartChannel.Text,
      FixtureModeTag: GetSelectedTag(editor.FixtureMode) ?? string.Empty,
      LightOrder: editor.LightOrder.Text);
  }

  private void ApplyProfileToEditor(ShowProfile profile)
  {
    var state = ProfileEditorMapper.FromProfile(profile);
    ProfileNameTextBox.Text = state.Name;
    BindAddressTextBox.Text = state.BindAddress;
    ArtNetPortTextBox.Text = state.ArtNetPort;
    OutputFpsTextBox.Text = state.OutputFps;
    TimeoutSecondsTextBox.Text = state.TimeoutSeconds;
    SelectComboTag(TimeoutModeComboBox, state.TimeoutModeTag);

    var editors = GetHubEditors().ToList();
    for (int index = 0; index < editors.Count; index++)
    {
      ApplyHubStateToEditor(state.Hubs[index], editors[index]);
    }
  }

  private static void ApplyHubStateToEditor(HubEditorState state, HubEditorControls editor)
  {
    editor.Enabled.IsChecked = state.Enabled;
    editor.Name.Text = state.Name;
    editor.BridgeIp.Text = state.BridgeIp;
    editor.ApplicationKey.Password = state.ApplicationKey;
    editor.EntertainmentKey.Password = state.EntertainmentKey;
    editor.EntertainmentGroupId.Text = state.EntertainmentGroupId;
    editor.Universe.Text = state.Universe;
    editor.StartChannel.Text = state.StartChannel;
    SelectComboTag(editor.FixtureMode, state.FixtureModeTag);
    editor.LightOrder.Text = state.LightOrder;
  }

  private void ClearPairingState()
  {
    currentPairingResult = null;
    currentPairingRowIndex = null;
    suppressEntertainmentGroupSelection = true;
    try
    {
      EntertainmentGroupComboBox.ItemsSource = null;
    }
    finally
    {
      suppressEntertainmentGroupSelection = false;
    }

    PairedLightOrderTextBox.Text = string.Empty;
  }

  private void ApplyPairingToHub(HueBridgePairingResult pairingResult, int rowIndex, bool updateCredentials)
  {
    var editors = GetHubEditors();
    int clampedRowIndex = Math.Clamp(rowIndex, 0, editors.Count - 1);
    var editor = editors[clampedRowIndex];
    var group = EntertainmentGroupComboBox.SelectedItem as HueEntertainmentGroupInfo
      ?? pairingResult.EntertainmentGroups.FirstOrDefault();

    if (updateCredentials)
    {
      editor.Enabled.IsChecked = true;
      editor.Name.Text = string.IsNullOrWhiteSpace(pairingResult.BridgeName)
        ? $"Hue hub {clampedRowIndex + 1}"
        : pairingResult.BridgeName;
      editor.BridgeIp.Text = pairingResult.BridgeIp;
      editor.ApplicationKey.Password = pairingResult.ApplicationKey;
      editor.EntertainmentKey.Password = pairingResult.EntertainmentKey;
    }

    if (group != null)
    {
      editor.EntertainmentGroupId.Text = group.Id.ToString();
      editor.LightOrder.Text = string.Join(",", group.ChannelIds);
      PairedLightOrderTextBox.Text = string.Join(",", group.ChannelIds);
    }

    currentProfile = BuildProfileFromEditor();
    currentProfile.HubMappings[clampedRowIndex].HueBridgeId = pairingResult.BridgeId;
    ApplyProfileToEditor(currentProfile);
  }

  private IReadOnlyList<HubEditorControls> GetHubEditors()
  {
    return new[]
    {
      new HubEditorControls(Hub1EnabledCheckBox, Hub1NameTextBox, Hub1BridgeIpTextBox, Hub1ApplicationKeyBox, Hub1EntertainmentKeyBox, Hub1EntertainmentGroupIdTextBox, Hub1UniverseTextBox, Hub1StartChannelTextBox, Hub1FixtureModeComboBox, Hub1LightOrderTextBox),
      new HubEditorControls(Hub2EnabledCheckBox, Hub2NameTextBox, Hub2BridgeIpTextBox, Hub2ApplicationKeyBox, Hub2EntertainmentKeyBox, Hub2EntertainmentGroupIdTextBox, Hub2UniverseTextBox, Hub2StartChannelTextBox, Hub2FixtureModeComboBox, Hub2LightOrderTextBox),
      new HubEditorControls(Hub3EnabledCheckBox, Hub3NameTextBox, Hub3BridgeIpTextBox, Hub3ApplicationKeyBox, Hub3EntertainmentKeyBox, Hub3EntertainmentGroupIdTextBox, Hub3UniverseTextBox, Hub3StartChannelTextBox, Hub3FixtureModeComboBox, Hub3LightOrderTextBox),
      new HubEditorControls(Hub4EnabledCheckBox, Hub4NameTextBox, Hub4BridgeIpTextBox, Hub4ApplicationKeyBox, Hub4EntertainmentKeyBox, Hub4EntertainmentGroupIdTextBox, Hub4UniverseTextBox, Hub4StartChannelTextBox, Hub4FixtureModeComboBox, Hub4LightOrderTextBox)
    };
  }

  private int ParseSelectedHubRow()
  {
    var tag = (PairingHubRowComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    return int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowIndex) ? rowIndex : 0;
  }

  private int GetSelectedHubRowIndex()
  {
    return Math.Clamp(ParseSelectedHubRow(), 0, GetHubEditors().Count - 1);
  }

  private void ThemeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
  {
    ApplyTheme(ThemeToggleSwitch.IsOn ? ElementTheme.Dark : ElementTheme.Light);
  }

  private void ApplyTheme(ElementTheme theme)
  {
    RootShell.RequestedTheme = theme;
    var palette = theme == ElementTheme.Dark ? AppThemePalettes.Dark : AppThemePalettes.Light;

    RootShell.Background = new SolidColorBrush(ToColor(palette.Background));
    SetBrush("AppBackgroundBrush", palette.Background);
    SetBrush("SurfaceBrush", palette.Surface);
    SetBrush("SurfaceAltBrush", palette.SurfaceAlt);
    SetBrush("BorderBrush", palette.Border);
    SetBrush("TextPrimaryBrush", palette.TextPrimary);
    SetBrush("TextSecondaryBrush", palette.TextSecondary);
    SetBrush("AccentBrush", palette.Accent);
    SetBrush("SuccessBrush", palette.Success);
    SetBrush("WarningBrush", palette.Warning);
  }

  private void SetBrush(string key, string color)
  {
    if (RootShell.Resources[key] is SolidColorBrush brush)
      brush.Color = ToColor(color);
  }

  private static Windows.UI.Color ToColor(string value)
  {
    var hex = value.TrimStart('#');
    byte r = Convert.ToByte(hex[..2], 16);
    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
    byte b = Convert.ToByte(hex.Substring(4, 2), 16);

    return Windows.UI.Color.FromArgb(255, r, g, b);
  }

  private static string? GetSelectedTag(ComboBox comboBox)
  {
    return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
  }

  private static void SelectComboTag(ComboBox comboBox, string tag)
  {
    foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
    {
      if (string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
      {
        comboBox.SelectedItem = item;
        return;
      }
    }

    comboBox.SelectedIndex = 0;
  }

  private sealed record HubEditorControls(
    CheckBox Enabled,
    TextBox Name,
    TextBox BridgeIp,
    PasswordBox ApplicationKey,
    PasswordBox EntertainmentKey,
    TextBox EntertainmentGroupId,
    TextBox Universe,
    TextBox StartChannel,
    ComboBox FixtureMode,
    TextBox LightOrder);
}
