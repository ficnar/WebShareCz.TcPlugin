using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient;
using MaFi.WebShareCz.ApiClient.Entities;
using TcPluginBase;
using TcPluginBase.FileSystem;
using MaFi.WebShareCz.TcPlugin.UI;

namespace MaFi.WebShareCz.TcPlugin
{
    internal sealed class WsAccountAccessor : FindData
    {
        private readonly WsAccount _account;
        private readonly TcUIProvider _uiProvider;
        private readonly WsApiClient _apiClient;
        private readonly TcSecretStore _secretStore;

        public static bool TryRegisterAccount(WsAccountRepository accountRepository, TcUIProvider uiProvider, WsAccountLoginInfo userCredential, out WsAccountAccessor accountAccessor)
        {
            WsAccountRepository.SuccessAccountRegistrationInfo successRegistration;
            using (ThreadKeeper exec = new ThreadKeeper())
            {
                successRegistration = exec.ExecAsync((cancellationToken) => accountRepository.TryRegisterAccount(userCredential));
            }
            if (successRegistration != null)
            {
                accountAccessor = new WsAccountAccessor(successRegistration.Account, uiProvider, successRegistration.ConnectedApiClient);
                return true;
            }
            accountAccessor = null;
            return false;
        }

        public WsAccountAccessor(WsAccount account, TcUIProvider uiProvider, Guid deviceUuid) : this(account, uiProvider, new WsApiClient(deviceUuid))
        {
        }

        private WsAccountAccessor(WsAccount account, TcUIProvider uiProvider, WsApiClient apiClient) : base(account.UserName, FileAttributes.Directory)
        {
            _account = account;
            _uiProvider = uiProvider;
            _apiClient = apiClient;
            _secretStore = new TcSecretStore(account, _uiProvider);
        }

        public bool UnRegisterAccount(WsAccountRepository accountRepository)
        {
            if (accountRepository.UnRegisterAccount(_account))
            {
                if (_apiClient.IsLoggedIn)
                {
                    using (ThreadKeeper exec = new ThreadKeeper())
                    {
                        exec.ExecAsync((cancellationToken) => _apiClient.Logout());
                    }
                }
                return true;
            }
            return false;
        }

        public IDisposableEnumerable<FindData> GetFolderItems(WsPath folderPath)
        {
            try
            {
                return ExecuteAsync(folderPath, async () => (IDisposableEnumerable<FindData>)new FolderItems(await _apiClient.GetFolderItems(folderPath.GetFolderPath())));
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                ShowError("Get folder content error", ex, false);
                return FolderItems.Empty;
            }
        }

        public IDisposableEnumerable<FindData> GetFolderAllFilesRecursive(WsPath folderPath, int depth = int.MaxValue)
        {
            try
            {
                return ExecuteAsync(folderPath, async () => (IDisposableEnumerable<FindData>)new FolderItems(await _apiClient.GetFolderAllFilesRecursive(folderPath.GetFolderPath(), depth)));
            }
            catch (Exception ex)
            {
                ShowError("Get all files recursive error", ex, false);
                return FolderItems.Empty;
            }
        }

        public async Task<FileSystemExitCode> DownloadFile(WsPath sourceFileName, FileInfo targetFileName, bool overwrite, IProgress<int> progress, bool deleteAfter, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureLogin();
                WsFile file = await _apiClient.GetFile(sourceFileName.GetFilePath());
                FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;

                using (FileStream targetStream = targetFileName.Open(mode, FileAccess.Write))
                {
                    await file.Download(targetStream, cancellationToken, progress);
                }
                targetFileName.CreationTime = targetFileName.LastWriteTime = file.Created;
                if (deleteAfter)
                    await file.Delete();
                return FileSystemExitCode.OK;
            }
            catch (TaskCanceledException)
            {
                targetFileName.Delete();
                return FileSystemExitCode.UserAbort;
            }
            catch (FileNotFoundException)
            {
                return FileSystemExitCode.FileNotFound;
            }
            catch (Exception ex)
            {
                targetFileName.Delete();
                if (ShowError("Download file error", ex, true) == false)
                    return FileSystemExitCode.UserAbort;
                return FileSystemExitCode.ReadError;
            }
        }

        public async Task<FileSystemExitCode> UploadFile(FileInfo sourceFileName, WsPath targetFileName, bool overwrite, IProgress<int> progress, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureLogin();
                bool uploadDirect = overwrite;
                WsFilePath targetFilePath = targetFileName.GetFilePath();
                if (overwrite == false)
                {
                    WsFile file = await _apiClient.FindFile(targetFilePath);
                    if (file != null)
                    {
                        if (overwrite == false)
                            return FileSystemExitCode.FileExists;
                        using (FileStream sourceStream = sourceFileName.OpenRead())
                        {
                            await file.Replace(sourceStream, cancellationToken, progress);
                        }
                    }
                    else
                        uploadDirect = true;
                }
                if (uploadDirect)
                {
                    using (FileStream sourceStream = sourceFileName.OpenRead())
                    {
                        await _apiClient.UploadFile(sourceStream, sourceStream.Length, targetFilePath, cancellationToken, progress);
                    }
                }
                return FileSystemExitCode.OK;
            }
            catch (TaskCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
            catch (FileNotFoundException)
            {
                return FileSystemExitCode.FileNotFound;
            }
            // TODO: dialog for file password, Exception throw in WsApiClient.CheckResultStatus
            //catch (??)
            //{
            //    return ??;
            //}
            catch (Exception ex)
            {
                if (ShowError("Upload file error", ex, true) == false)
                    return FileSystemExitCode.UserAbort;
                return FileSystemExitCode.ReadError;
            }
        }

        public WsFolder CreateFolder(WsPath folderPath)
        {
            try
            {
                return ExecuteAsync(folderPath, () => _apiClient.CreateFolder(folderPath, folderPath.IsPrivate));
            }
            catch (Exception ex)
            {
                ShowError("Create folder error", ex, false);
                return null;
            }
        }

        public bool DeleteFolder(WsPath folderPath)
        {
            try
            {
                return ExecuteAsync(folderPath, async () =>
                {
                    WsFolder folder = await _apiClient.FindFolder(folderPath.GetFolderPath());
                    if (folder == null)
                        return false;
                    await folder.Delete();
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowError("Delete folder error", ex, false);
                return false;
            }
        }

        public bool DeleteFile(WsPath filePath)
        {
            try
            {
                return ExecuteAsync(filePath, async () =>
                {
                    WsFile file = await _apiClient.FindFile(filePath.GetFilePath());
                    if (file == null)
                        return false;
                    await file.Delete();
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowError("Delete file error", ex, false);
                return false;
            }
        }

        public FileSystemExitCode MoveOrRenameItem(WsPath sourcePath, WsPath targetPath, bool overwrite, bool sourceIsFolder)
        {
            try
            {
                return ExecuteAsync(sourcePath, async () =>
                {
                    WsItem sourceItem;
                    if (sourceIsFolder)
                    {
                        sourceItem = await _apiClient.FindFolder(sourcePath.GetFolderPath());
                        if (sourceItem != null)
                        {
                            if (overwrite == false)
                            {
                                if (await _apiClient.FindFolder(targetPath.GetFolderPath()) != null)
                                    return FileSystemExitCode.FileExists; // TODO: not work for renaming to existing folder
                            }
                        }
                    }
                    else
                    {
                        sourceItem = await _apiClient.FindFile(sourcePath.GetFilePath());
                        if (sourceItem != null)
                        {
                            if (overwrite == false)
                            {
                                if (await _apiClient.FindFile(targetPath.GetFilePath()) != null)
                                    return FileSystemExitCode.FileExists; // TODO: not work for renaming to existing file
                            }
                        }
                    }
                    if (sourceItem == null)
                        return FileSystemExitCode.FileNotFound;
                    
                    if (sourcePath.Parent.Path == targetPath.Parent.Path)
                        await sourceItem.Rename(targetPath.Name);
                    else
                    {
                        WsFolder targetFolder = await _apiClient.FindFolder(targetPath.Parent.GetFolderPath());
                        if (targetFolder == null)
                            return FileSystemExitCode.FileNotFound;
                        await sourceItem.Move(targetFolder);
                    }
                    return FileSystemExitCode.OK;
                });
            }
            catch (Exception ex)
            {
                ShowError("Move/rename file/folder error", ex, false);
                return FileSystemExitCode.WriteError;
            }
        }

        public FileSystemExitCode CopyFile(WsPath sourceFilePath, WsPath targetPath, bool overwrite, IProgress<int> progress)
        {
            try
            {
                using (ThreadKeeper exec = new ThreadKeeper())
                {
                    return exec.ExecAsync(async (cancellationToken) =>
                    {
                        await EnsureLogin();
                        WsFile sourceFile = await _apiClient.FindFile(sourceFilePath.GetFilePath());
                        if (sourceFile != null)
                        {
                            if (overwrite == false)
                            {
                                if (await _apiClient.FindFile(targetPath.GetFilePath()) != null)
                                    return FileSystemExitCode.FileExists;
                            }
                        }

                        if (sourceFile == null)
                            return FileSystemExitCode.FileNotFound;
                        WsFolder targetFolder = await _apiClient.FindFolder(targetPath.Parent.GetFolderPath());
                        if (targetFolder == null)
                            return FileSystemExitCode.FileNotFound;

                        await sourceFile.Copy(targetFolder, cancellationToken, new ThreadKeeperCancellableProgress(exec, progress));
                        return FileSystemExitCode.OK;
                    });
                }
            }
            catch (TaskCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
            catch (Exception ex)
            {
                ShowError("Copy file error", ex, false);
                return FileSystemExitCode.WriteError;
            }
        }

        private async Task EnsureLogin()
        {
            if (_apiClient.IsLoggedIn == false)
            {
                while ((await _apiClient.Login(this.FileName, _secretStore, _secretStore)) == false)
                {
                    _uiProvider.ShowMessage(Resources.TextResource.WrongLogin, false);
                    // broken by OperationCanceledException
                }
            }
        }

        private T ExecuteAsync<T>(WsPath path, Func<Task<T>> asyncFunc)
        {
            using (ThreadKeeper exec = new ThreadKeeper())
            {
                try
                {
                    return exec.ExecAsync(async (cancellationToken) => 
                    {
                        await EnsureLogin();
                        return await asyncFunc();
                    });
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (ex.InnerException != null)
                            throw ex.InnerException;
                        throw;
                    }
                    catch (FileNotFoundException)
                    {
                        ShowError($"Path {path} not found.", null, false);
                        return default(T);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        ShowError($"Folder {path} not found.", null, false);
                        return default(T);
                    }
                }
            }
        }

        private bool ShowError(string baseMessage, Exception ex, bool allowCancel)
        {
            string errorMessages = ex == null ? "" : GetExceptionMessageRecursive(ex);
            return _uiProvider.ShowMessage($"{baseMessage}{errorMessages}", allowCancel);
        }

        private string GetExceptionMessageRecursive(Exception exception, string message = ":")
        {
            if (exception.InnerException != null)
                message += "\r\n" + GetExceptionMessageRecursive(exception.InnerException, message);
            message += $"\r\n{exception.GetType().Name}: {exception.Message}";
            return message;
        }

        public override string ToString()
        {
            return _account.ToString();
        }

        public override int GetHashCode()
        {
            return _account.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is WsAccountAccessor accountAccessor)
                return _account.Equals(accountAccessor._account);
            return false;
        }

        private sealed class FolderItems : IDisposableEnumerable<FindData>
        {
            private readonly WsItemsReader _itemsReader;

            public static FolderItems Empty { get; } = new FolderItems(null);

            public FolderItems(WsItemsReader itemsReader)
            {
                _itemsReader = itemsReader;
            }

            public IEnumerator<FindData> GetEnumerator()
            {
                if (_itemsReader != null)
                {
                    foreach (WsItem item in _itemsReader)
                    {
                        yield return item.ConvertToFindData();
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public void Dispose()
            {
                _itemsReader?.Dispose();
            }
        }

        private sealed class ThreadKeeperCancellableProgress : IProgress<int>
        {
            private readonly ThreadKeeper _exec;
            private readonly IProgress<int> _parrentProgress;

            public ThreadKeeperCancellableProgress(ThreadKeeper exec, IProgress<int> parrentProgress)
            {
                _exec = exec;
                _parrentProgress = parrentProgress;
            }
            public void Report(int value)
            {
                _exec.RunInMainThread(() => {
                    try
                    {
                        _parrentProgress.Report(value);
                    }
                    catch (TaskCanceledException)
                    {
                        _exec.Cancel();
                    }
                });
            }
        }
    }
}
