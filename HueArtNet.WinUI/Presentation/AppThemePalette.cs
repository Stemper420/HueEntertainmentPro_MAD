namespace HueArtNet.WinUI.Presentation;

internal sealed record AppThemePalette(
  string Background,
  string Surface,
  string SurfaceAlt,
  string Border,
  string TextPrimary,
  string TextSecondary,
  string Accent,
  string Success,
  string Warning);

internal static class AppThemePalettes
{
  public static AppThemePalette Light { get; } = new(
    Background: "#F6F8FB",
    Surface: "#FFFFFF",
    SurfaceAlt: "#EEF4FA",
    Border: "#D6DEE8",
    TextPrimary: "#111827",
    TextSecondary: "#5B6575",
    Accent: "#0078D4",
    Success: "#107C10",
    Warning: "#B7791F");

  public static AppThemePalette Dark { get; } = new(
    Background: "#0F141B",
    Surface: "#171E27",
    SurfaceAlt: "#202A36",
    Border: "#344154",
    TextPrimary: "#F4F7FB",
    TextSecondary: "#AEB8C8",
    Accent: "#4DA3FF",
    Success: "#5CCB5F",
    Warning: "#F2C94C");
}
