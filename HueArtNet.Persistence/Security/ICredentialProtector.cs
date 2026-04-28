namespace HueArtNet.Persistence.Security;

public interface ICredentialProtector
{
  string Protect(string value);
  string Unprotect(string protectedValue);
}
