using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace HueArtNet.Persistence.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialProtector : ICredentialProtector
{
  public string Protect(string value)
  {
    if (string.IsNullOrEmpty(value))
      return string.Empty;

    byte[] plainBytes = Encoding.UTF8.GetBytes(value);
    byte[] protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(protectedBytes);
  }

  public string Unprotect(string protectedValue)
  {
    if (string.IsNullOrEmpty(protectedValue))
      return string.Empty;

    byte[] protectedBytes = Convert.FromBase64String(protectedValue);
    byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(plainBytes);
  }
}
