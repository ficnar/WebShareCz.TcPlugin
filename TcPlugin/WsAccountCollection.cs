using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MaFi.WebShareCz.TcPlugin.UI;
using TcPluginBase.FileSystem;
using MaFi.WebShareCz.ApiClient;
using MaFi.WebShareCz.ApiClient.Entities;

namespace MaFi.WebShareCz.TcPlugin
{
    internal sealed class WsAccountCollection : IEnumerable<WsAccountAccessor>
    {
        private readonly TcUIProvider _uiProvider;
        private readonly WsAccountRepository _accountRepository;
        private readonly List<WsAccountAccessor> _accountAccessors;

        public WsAccountCollection(TcUIProvider uiProvider)
        {
            _uiProvider = uiProvider;
            _accountRepository = new WsAccountRepository(new TcDataProtector(), "TotalCommander");
            _accountAccessors = new List<WsAccountAccessor>(_accountRepository.Select(a => new WsAccountAccessor(a, uiProvider, _accountRepository.GetDeviceUuid())).ToArray());
        }

        public ExecResult AddNewAccount(string defaultUserName = null)
        {
            WsAccountLoginInfo userCredential = _uiProvider.PromptUserCredential(defaultUserName);
            if (userCredential != null)
            {
                if (_accountAccessors.Exists(a => a.FileName.Equals(userCredential.UserName, StringComparison.InvariantCultureIgnoreCase)))
                    _uiProvider.ShowMessage(string.Format(Resources.TextResource.AccountExists, userCredential.UserName), false);
                else if (WsAccountAccessor.TryRegisterAccount(_accountRepository, _uiProvider, userCredential, out WsAccountAccessor newAccountAccessor))
                {
                    _accountAccessors.Add(newAccountAccessor);
                    return ExecResult.SymLink(@"/");
                }
                else
                    _uiProvider.ShowMessage(Resources.TextResource.WrongLogin, false);
            }
            return ExecResult.Ok;
        }

        public bool UnRegisterAccount(WsAccountAccessor accountAccessor)
        {
            if (accountAccessor.UnRegisterAccount(_accountRepository))
            {
                _accountAccessors.Remove(accountAccessor);
                return true;
            }
            return false;
        }

        public IEnumerator<WsAccountAccessor> GetEnumerator()
        {
            return _accountAccessors.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public WsAccountAccessor this[int index]
        {
            get => _accountAccessors[index];
        }


        public WsAccountAccessor this[string userName]
        {
            get => _accountAccessors.First(a => a.FileName.Equals(userName, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
