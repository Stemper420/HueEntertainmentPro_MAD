using System.Globalization;
using HueArtNet.Core.Profiles;
using HueArtNet.Hue.Runtime;
using HueArtNet.Hue.Setup;
using HueArtNet.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

  public MainWindow(
    IServiceScopeFactory scopeFactory,
    ArtNetShowRuntime showRuntime,
    IHueBridgeSetupService bridgeSetupService)
  {
    this.scopeFactory = scopeFactory;
    this.showRuntime = showRuntime;
    this.bridgeSetupService = bridgeSetupService;
    InitializeComponent();
    Activated += MainWindow_Activated;
  }

  private async void NewTemplate_Click(object sender, RoutedEventArgs e)
  {
    ClearPairingState();
    currentProfile = CreateProfileTemplate();
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

    try
    {
      isPairing = true;
      PairSelectedBridgeButton.IsEnabled = false;
      ClearPairingState();
      if (DiscoveredBridgeComboBox.SelectedItem is not HueBridgeCandidate bridge)
      {
        await RefreshStatusAsync("Select a discovered Hue bridge before pairing.");
        return;
      }

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
      await RefreshStatusAsync($"Paired {result.BridgeName}. Select an Entertainment group if more than one was found.");
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

  private void EntertainmentGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (!suppressEntertainmentGroupSelection && currentPairingResult != null && currentPairingRowIndex.HasValue)
      ApplyPairingToHub(currentPairingResult, currentPairingRowIndex.Value, updateCredentials: false);
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
      currentProfile = CreateProfileTemplate();
      ApplyProfileToEditor(currentProfile);
      await RefreshStatusAsync($"Profile load failed: {ex.Message}");
    }
  }

  private async Task LoadEditorAsync(string? message = null)
  {
    currentProfile = (await WithRepositoryAsync(repository => repository.GetAllAsync(CancellationToken.None))).FirstOrDefault() ?? CreateProfileTemplate();
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
    await RefreshStatusAsync($"Saved profile '{profile.Name}'.");
  }

  private async Task RefreshStatusAsync(string? message = null, IEnumerable<string>? validationErrors = null)
  {
    var profiles = await WithRepositoryAsync(repository => repository.GetAllAsync(CancellationToken.None));
    var runtimeStatus = showRuntime.GetStatus();
    var lines = new List<string>();

    if (!string.IsNullOrWhiteSpace(message))
      lines.Add(message);

    if (validationErrors != null)
    {
      foreach (var error in validationErrors)
        lines.Add($"- {error}");
    }

    lines.Add($"Stored profiles: {profiles.Count}");
    foreach (var profile in profiles)
    {
      var validation = ShowProfileValidator.Validate(profile);
      lines.Add($"- {profile.Name}: {(validation.IsValid ? "valid" : "invalid")} / universes {string.Join(", ", validation.ActiveUniverses)}");
      foreach (var error in validation.Errors)
        lines.Add($"  {error}");
    }

    lines.Add($"Runtime running: {runtimeStatus.IsRunning}");
    if (runtimeStatus.IsRunning)
    {
      lines.Add($"Profile: {runtimeStatus.ProfileName}");
      lines.Add($"Art-Net: {runtimeStatus.BindAddress ?? "0.0.0.0"}:{runtimeStatus.Port} / universes {string.Join(", ", runtimeStatus.ArtNet.ActiveUniverses)}");
      lines.Add($"Packets: {runtimeStatus.ArtNet.AcceptedPackets} accepted, {runtimeStatus.ArtNet.IgnoredPackets} ignored, {runtimeStatus.ArtNet.PacketsPerSecond:F1}/s");
      lines.Add($"Timeout: {(runtimeStatus.IsTimedOut ? "yes" : "no")}");
      if (!string.IsNullOrWhiteSpace(runtimeStatus.LastError))
        lines.Add($"Runtime error: {runtimeStatus.LastError}");
    }

    lines.Add($"Hue output running: {runtimeStatus.HueOutput.IsRunning}");
    foreach (var session in runtimeStatus.HueOutput.Sessions)
      lines.Add($"- {session.Name} ({session.BridgeIp}): {(session.IsConnected ? "connected" : "offline")} {session.LastError}");

    StatusText.Text = string.Join(Environment.NewLine, lines);
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
    var source = currentProfile ?? CreateProfileTemplate();
    return new ShowProfile
    {
      Id = source.Id,
      Name = ProfileNameTextBox.Text.Trim(),
      IsDefault = true,
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = NormalizeBindAddress(BindAddressTextBox.Text),
        Port = ParseInt(ArtNetPortTextBox, 6454)
      },
      Output = new OutputConfig
      {
        FramesPerSecond = ParseInt(OutputFpsTextBox, 20),
        BrightnessLimit = source.Output.BrightnessLimit
      },
      FailSafe = new FailSafeConfig
      {
        Mode = ParseFailSafeMode(TimeoutModeComboBox),
        Timeout = TimeSpan.FromSeconds(Math.Max(ParseDouble(TimeoutSecondsTextBox, 2), 0.1))
      },
      HubMappings = GetHubEditors()
        .Select((editor, index) => BuildMapping(source.HubMappings.ElementAtOrDefault(index), editor, index))
        .ToList()
    };
  }

  private static HubMapping BuildMapping(HubMapping? source, HubEditorControls editor, int index)
  {
    return new HubMapping
    {
      Id = source?.Id ?? Guid.NewGuid(),
      Name = string.IsNullOrWhiteSpace(editor.Name.Text) ? $"Hue hub {index + 1}" : editor.Name.Text.Trim(),
      BridgeId = source == null || source.BridgeId == Guid.Empty ? Guid.NewGuid() : source.BridgeId,
      HueBridgeId = source?.HueBridgeId ?? string.Empty,
      BridgeIp = editor.BridgeIp.Text.Trim(),
      ApplicationKey = editor.ApplicationKey.Password.Trim(),
      EntertainmentKey = editor.EntertainmentKey.Password.Trim(),
      EntertainmentGroupId = Guid.TryParse(editor.EntertainmentGroupId.Text.Trim(), out var groupId) ? groupId : Guid.Empty,
      Universe = ParseInt(editor.Universe, index),
      StartChannel = ParseInt(editor.StartChannel, 1),
      FixtureMode = ParseFixtureMode(editor.FixtureMode),
      LightOrder = ParseLightOrder(editor.LightOrder.Text),
      Enabled = editor.Enabled.IsChecked == true,
      Required = source?.Required ?? false
    };
  }

  private void ApplyProfileToEditor(ShowProfile profile)
  {
    ProfileNameTextBox.Text = profile.Name;
    BindAddressTextBox.Text = profile.ArtNetInput.BindAddress ?? string.Empty;
    ArtNetPortTextBox.Text = profile.ArtNetInput.Port.ToString(CultureInfo.InvariantCulture);
    OutputFpsTextBox.Text = profile.Output.FramesPerSecond.ToString(CultureInfo.InvariantCulture);
    TimeoutSecondsTextBox.Text = profile.FailSafe.Timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    SelectComboTag(TimeoutModeComboBox, profile.FailSafe.Mode.ToString());

    var editors = GetHubEditors().ToList();
    for (int index = 0; index < editors.Count; index++)
    {
      var mapping = profile.HubMappings.ElementAtOrDefault(index) ?? CreateTemplateMapping(index);
      ApplyMappingToEditor(mapping, editors[index]);
    }
  }

  private static void ApplyMappingToEditor(HubMapping mapping, HubEditorControls editor)
  {
    editor.Enabled.IsChecked = mapping.Enabled;
    editor.Name.Text = mapping.Name;
    editor.BridgeIp.Text = mapping.BridgeIp;
    editor.ApplicationKey.Password = mapping.ApplicationKey;
    editor.EntertainmentKey.Password = mapping.EntertainmentKey;
    editor.EntertainmentGroupId.Text = mapping.EntertainmentGroupId == Guid.Empty ? string.Empty : mapping.EntertainmentGroupId.ToString();
    editor.Universe.Text = mapping.Universe.ToString(CultureInfo.InvariantCulture);
    editor.StartChannel.Text = mapping.StartChannel.ToString(CultureInfo.InvariantCulture);
    SelectComboTag(editor.FixtureMode, mapping.FixtureMode.ToString());
    editor.LightOrder.Text = string.Join(",", mapping.LightOrder);
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

  private static ShowProfile CreateProfileTemplate()
  {
    return new ShowProfile
    {
      Name = "Four hub Art-Net profile",
      IsDefault = true,
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = "0.0.0.0",
        Port = 6454
      },
      Output = new OutputConfig
      {
        FramesPerSecond = 20,
        BrightnessLimit = 1
      },
      FailSafe = new FailSafeConfig
      {
        Mode = FailSafeMode.HoldLastFrame,
        Timeout = TimeSpan.FromSeconds(2)
      },
      HubMappings = Enumerable.Range(0, 4).Select(CreateTemplateMapping).ToList()
    };
  }

  private static HubMapping CreateTemplateMapping(int index)
  {
    return new HubMapping
    {
      Name = $"Hue hub {index + 1}",
      BridgeId = Guid.NewGuid(),
      HueBridgeId = string.Empty,
      BridgeIp = $"192.168.1.{20 + index}",
      Universe = index,
      StartChannel = 1,
      FixtureMode = FixtureMode.Rgb3,
      LightOrder = Enumerable.Range(1, 10).ToList(),
      Enabled = true
    };
  }

  private static int ParseInt(TextBox textBox, int fallback)
  {
    return int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
      ? value
      : fallback;
  }

  private static double ParseDouble(TextBox textBox, double fallback)
  {
    return double.TryParse(textBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
      ? value
      : fallback;
  }

  private static List<int> ParseLightOrder(string value)
  {
    var parsed = value
      .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lightId) ? lightId : 0)
      .Where(lightId => lightId > 0)
      .Distinct()
      .ToList();

    return parsed.Count > 0 ? parsed : Enumerable.Range(1, 10).ToList();
  }

  private static string? NormalizeBindAddress(string value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
  }

  private static FixtureMode ParseFixtureMode(ComboBox comboBox)
  {
    var tag = GetSelectedTag(comboBox);
    return Enum.TryParse<FixtureMode>(tag, out var mode) ? mode : FixtureMode.Rgb3;
  }

  private static FailSafeMode ParseFailSafeMode(ComboBox comboBox)
  {
    var tag = GetSelectedTag(comboBox);
    return Enum.TryParse<FailSafeMode>(tag, out var mode) ? mode : FailSafeMode.HoldLastFrame;
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
