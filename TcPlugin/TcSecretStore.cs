using System;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.TcPlugin.UI;
using MaFi.WebShareCz.ApiClient.Security;

namespace MaFi.WebShareCz.TcPlugin
{
    internal sealed class TcSecretStore : ISecretProvider, ISecretPersistor
    {
        private readonly WsAccount _account;
        private readonly TcUIProvider _uiProvider;

        public TcSecretStore(WsAccount account, TcUIProvider uiProvider)
        {
            _account = account;
            _uiProvider = uiProvider;
        }

        public Task<string> GetPassword()
        {
            string password = _uiProvider.PromptPassword(_account.UserName);
            if (string.IsNullOrEmpty(password))
                throw new OperationCanceledException();
            return Task.FromResult(password);
        }

        public void SaveUserPasswordHash(string userPasswordHash)
        {
            _account.SaveUserPasswordHash(userPasswordHash);
        }

        public bool TryGetUserPasswordHash(out string userPasswordHash)
        {
            return _account.TryGetUserPasswordHash(out userPasswordHash);
        }
    }
}
