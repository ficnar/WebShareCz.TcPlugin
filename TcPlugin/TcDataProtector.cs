using MaFi.WebShareCz.ApiClient.Security;
using System.Security.Cryptography;

namespace MaFi.WebShareCz.TcPlugin
{
    internal sealed class TcDataProtector : IDataProtector
    {
        private static readonly byte[] _entropy = new byte[] { 26, 151, 9, 92, 249, 94, 207, 16 };

        public byte[] Protect(byte[] userData)
        {
            return ProtectedData.Protect(userData, _entropy, DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] encryptedData)
        {
            return ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);
        }
    }
}
