﻿using System;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.DialogResults;
using Neo.Gui.Base.Interfaces;
using Neo.UI.Base.MVVM;

namespace Neo.UI.Wallets
{
    public class OpenWalletViewModel : ViewModelBase, IDialogViewModel<OpenWalletDialogResult>
    {
        #region Private Fields
        private string walletPath;
        private string password;
        private bool repairMode;

        private bool confirmed;
        #endregion

        #region Public Properties 
        public string WalletPath
        {
            get => this.walletPath;
            set
            {
                if (this.walletPath == value) return;

                this.walletPath = value;

                NotifyPropertyChanged();

                // Update dependent property
                NotifyPropertyChanged(nameof(this.ConfirmEnabled));
            }
        }

        public bool RepairMode
        {
            get => this.repairMode;
            set
            {
                if (this.repairMode == value) return;

                this.repairMode = value;

                NotifyPropertyChanged();
            }
        }

        public bool ConfirmEnabled
        {
            get
            {
                if (string.IsNullOrEmpty(this.WalletPath) || string.IsNullOrEmpty(this.password))
                {
                    return false;
                }

                return true;
            }
        }

        public ICommand GetWalletPathCommand => new RelayCommand(this.GetWalletPath);

        public ICommand ConfirmCommand => new RelayCommand(this.Confirm);
        #endregion

        #region IDialogViewModel implementation 
        public event EventHandler<OpenWalletDialogResult> SetDialogResult;

        public OpenWalletDialogResult DialogResult { get; private set; }
        #endregion

        #region Public Methods 
        public void UpdatePassword(string updatedPassword)
        {
            this.password = updatedPassword;

            // Update dependent property
            NotifyPropertyChanged(nameof(this.ConfirmEnabled));
        }

        public bool GetWalletOpenInfo(out string path, out string walletPassword, out bool openInRepairMode)
        {
            path = null;
            walletPassword = null;
            openInRepairMode = false;

            if (!this.confirmed) return false;

            path = this.walletPath;
            walletPassword = this.password;
            openInRepairMode = this.repairMode;

            return true;
        }
        #endregion

        #region Private Methods 
        private void GetWalletPath()
        {
            var openFileDialog = new OpenFileDialog
            {
                DefaultExt = "db3",
                Filter = "Wallet File|*.db3"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.WalletPath = openFileDialog.FileName;
            }
        }

        private void Confirm()
        {
            if (!this.ConfirmEnabled) return;

            this.confirmed = true;

            if (this.SetDialogResult != null)
            {
                var dialogResult = new OpenWalletDialogResult(
                    this.WalletPath, 
                    this.password, 
                    this.RepairMode);
                this.SetDialogResult(this, dialogResult);
            }

            this.TryClose();
        }
        #endregion
    }
}