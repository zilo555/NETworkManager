﻿using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.SimpleChildWindow;
using NETworkManager.Localization.Resources;
using NETworkManager.Profiles;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace NETworkManager.ViewModels;

public class SettingsProfilesViewModel : ViewModelBase
{
    #region Variables

    private readonly IDialogCoordinator _dialogCoordinator;

    public Action CloseAction { get; set; }

    private string _location;

    public string Location
    {
        get => _location;
        set
        {
            if (value == _location)
                return;

            _location = value;
            OnPropertyChanged();
        }
    }

    private readonly ICollectionView _profileFiles;

    public ICollectionView ProfileFiles
    {
        get => _profileFiles;
        private init
        {
            if (value == _profileFiles)
                return;

            _profileFiles = value;
            OnPropertyChanged();
        }
    }

    private ProfileFileInfo _selectedProfileFile;

    public ProfileFileInfo SelectedProfileFile
    {
        get => _selectedProfileFile;
        set
        {
            if (Equals(value, _selectedProfileFile))
                return;

            _selectedProfileFile = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Constructor, LoadSettings

    public SettingsProfilesViewModel(IDialogCoordinator instance)
    {
        _dialogCoordinator = instance;

        ProfileFiles = new CollectionViewSource { Source = ProfileManager.ProfileFiles }.View;
        ProfileFiles.SortDescriptions.Add(
            new SortDescription(nameof(ProfileFileInfo.Name), ListSortDirection.Ascending));

        LoadSettings();
    }

    private void LoadSettings()
    {
        Location = ProfileManager.GetProfilesFolderLocation();
    }

    #endregion

    #region ICommands & Actions

    public ICommand OpenLocationCommand => new RelayCommand(_ => OpenLocationAction());

    private static void OpenLocationAction()
    {
        Process.Start("explorer.exe", ProfileManager.GetProfilesFolderLocation());
    }

    public ICommand AddProfileFileCommand => new RelayCommand(_ => AddProfileFileAction());

    private async void AddProfileFileAction()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.AddProfileFile
        };

        var profileFileViewModel = new ProfileFileViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

            ProfileManager.CreateEmptyProfileFile(instance.Name);
        }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); });

        customDialog.Content = new ProfileFileDialog
        {
            DataContext = profileFileViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    public ICommand EditProfileFileCommand => new RelayCommand(_ => EditProfileFileAction());

    private async void EditProfileFileAction()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.EditProfileFile
        };

        var profileFileViewModel = new ProfileFileViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

            ProfileManager.RenameProfileFile(SelectedProfileFile, instance.Name);
        }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); }, SelectedProfileFile);

        customDialog.Content = new ProfileFileDialog
        {
            DataContext = profileFileViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    public ICommand DeleteProfileFileCommand =>
        new RelayCommand(_ => DeleteProfileFileAction().ConfigureAwait(false), DeleteProfileFile_CanExecute);

    private bool DeleteProfileFile_CanExecute(object obj)
    {
        return ProfileFiles.Cast<ProfileFileInfo>().Count() > 1;
    }

    private Task DeleteProfileFileAction()
    {
        var childWindow = new OKCancelInfoMessageChildWindow();

        var childWindowViewModel = new OKCancelInfoMessageViewModel(_ =>
            {
                childWindow.IsOpen = false;
                ConfigurationManager.Current.IsChildWindowOpen = false;

                ProfileManager.DeleteProfileFile(SelectedProfileFile);
            }, _ =>
            {
                childWindow.IsOpen = false;
                ConfigurationManager.Current.IsChildWindowOpen = false;
            },
            Strings.DeleteProfileFileMessage, Strings.Delete);

        childWindow.Title = Strings.DeleteProfileFile;

        childWindow.DataContext = childWindowViewModel;

        ConfigurationManager.Current.IsChildWindowOpen = true;

        return (Application.Current.MainWindow as MainWindow).ShowChildWindowAsync(childWindow);
    }

    public ICommand EnableEncryptionCommand => new RelayCommand(_ => EnableEncryptionAction());

    private async void EnableEncryptionAction()
    {
        var settings = AppearanceManager.MetroDialog;

        settings.AffirmativeButtonText = Strings.OK;
        settings.NegativeButtonText = Strings.Cancel;
        settings.DefaultButtonFocus = MessageDialogResult.Affirmative;

        if (await _dialogCoordinator.ShowMessageAsync(this, Strings.Disclaimer, Strings.ProfileEncryptionDisclaimer, MessageDialogStyle.AffirmativeAndNegative, settings) != MessageDialogResult.Affirmative)
            return;

        var customDialog = new CustomDialog
        {
            Title = Strings.SetMasterPassword
        };

        var credentialsSetPasswordViewModel = new CredentialsSetPasswordViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

            try
            {
                ProfileManager.EnableEncryption(SelectedProfileFile, instance.Password);
            }
            catch (Exception ex)
            {
                var metroDialogSettings = AppearanceManager.MetroDialog;
                metroDialogSettings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(this, Strings.EncryptionError,
                    $"{Strings.EncryptionErrorMessage}\n\n{ex.Message}",
                    MessageDialogStyle.Affirmative, metroDialogSettings);
            }
        }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); });

        customDialog.Content = new CredentialsSetPasswordDialog
        {
            DataContext = credentialsSetPasswordViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    public ICommand ChangeMasterPasswordCommand => new RelayCommand(_ => ChangeMasterPasswordAction());

    private async void ChangeMasterPasswordAction()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.ChangeMasterPassword
        };

        var credentialsPasswordViewModel = new CredentialsChangePasswordViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

            try
            {
                ProfileManager.ChangeMasterPassword(SelectedProfileFile, instance.Password, instance.NewPassword);
            }
            catch (CryptographicException)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(this, Strings.WrongPassword,
                    Strings.WrongPasswordDecryptionFailedMessage, MessageDialogStyle.Affirmative,
                    settings);
            }
            catch (Exception ex)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(this, Strings.DecryptionError,
                    $"{Strings.DecryptionErrorMessage}\n\n{ex.Message}",
                    MessageDialogStyle.Affirmative, settings);
            }
        }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); });

        customDialog.Content = new CredentialsChangePasswordDialog
        {
            DataContext = credentialsPasswordViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    public ICommand DisableEncryptionCommand => new RelayCommand(_ => DisableEncryptionAction());

    private async void DisableEncryptionAction()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.MasterPassword
        };

        var credentialsPasswordViewModel = new CredentialsPasswordViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

            try
            {
                ProfileManager.DisableEncryption(SelectedProfileFile, instance.Password);
            }
            catch (CryptographicException)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(this, Strings.WrongPassword,
                    Strings.WrongPasswordDecryptionFailedMessage, MessageDialogStyle.Affirmative,
                    settings);
            }
            catch (Exception ex)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(this, Strings.DecryptionError,
                    $"{Strings.DecryptionErrorMessage}\n\n{ex.Message}",
                    MessageDialogStyle.Affirmative, settings);
            }
        }, async _1 => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); });

        customDialog.Content = new CredentialsPasswordDialog
        {
            DataContext = credentialsPasswordViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    #endregion
}