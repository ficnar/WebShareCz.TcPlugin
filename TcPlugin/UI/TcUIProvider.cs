using System.Net;
using MaFi.WebShareCz.ApiClient.Entities;
using TcPluginBase.FileSystem;

namespace MaFi.WebShareCz.TcPlugin.UI
{
    internal  class TcUIProvider
    {
        private readonly FsPrompt _prompt;

        public TcUIProvider(FsPrompt prompt)
        {
            _prompt = prompt;
        }

        public bool ShowMessage(string errorMessage, bool allowCancel)
        {
            if (allowCancel)
                return _prompt.MsgOkCancel(Resources.TextResource.WindowTitle, errorMessage);
            _prompt.MsgOk(Resources.TextResource.WindowTitle, errorMessage);
            return true;
        }

        public string PromptPassword(string userName)
        {
            return _prompt.AskPassword($"{Resources.TextResource.WindowTitle} - Heslo pro účet {userName}", "");
        }

        public WsAccountLoginInfo PromptUserCredential(string userName = null)
        {
            UserCredentialDialog dialog = new UserCredentialDialog(userName);
            if (dialog.ShowDialog() == true)
                return new WsAccountLoginInfo(dialog.TxtUserName.Text, dialog.TxtPassword.Password, dialog.CbRememberPassword.IsChecked ?? false);
            return null;
        }
    }
}
