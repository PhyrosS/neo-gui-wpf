﻿using System;
using System.Linq;
using System.Text;

using CERTENROLLLib;

using Neo.Cryptography.ECC;

using Neo.Gui.Base.Controllers;
using Neo.Gui.Base.Dialogs.Interfaces;
using Neo.Gui.Base.Dialogs.Results;
using Neo.Gui.Base.Managers;
using Neo.Gui.Base.Services;

using Neo.Gui.Wpf.MVVM;

namespace Neo.Gui.Wpf.Views.Wallets
{
    public class CertificateApplicationViewModel : ViewModelBase, IDialogViewModel<CertificateApplicationDialogResult>
    {
        private readonly IFileManager fileManager;
        private readonly IFileDialogService fileDialogService;
        private readonly IWalletController walletController;

        private ECPoint selectedPublicKey;
        private string cn;
        private string c;
        private string s;
        private string serialNumber;

        public CertificateApplicationViewModel(
            IFileManager fileManager,
            IFileDialogService fileDialogService,
            IWalletController walletController)
        {
            this.fileManager = fileManager;
            this.fileDialogService = fileDialogService;
            this.walletController = walletController;

            this.PublicKeys = this.walletController.GetContracts().Where(p => p.IsStandard).Select(p =>
                this.walletController.GetKey(p.PublicKeyHash).PublicKey).ToArray();
        }

        public ECPoint[] PublicKeys { get; }

        public ECPoint SelectedPublicKey
        {
            get => this.selectedPublicKey;
            set
            {
                if (Equals(this.selectedPublicKey, value)) return;

                this.selectedPublicKey = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.RequestCertificateEnabled));
            }
        }

        public string CN
        {
            get => this.cn;
            set
            {
                if (this.cn == value) return;

                this.cn = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.RequestCertificateEnabled));
            }
        }

        public string C
        {
            get => this.c;
            set
            {
                if (this.c == value) return;

                this.c = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.RequestCertificateEnabled));
            }
        }

        public string S
        {
            get => this.s;
            set
            {
                if (this.s == value) return;

                this.s = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.RequestCertificateEnabled));
            }
        }

        public string SerialNumber
        {
            get => this.serialNumber;
            set
            {
                if (this.serialNumber == value) return;

                this.serialNumber = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.RequestCertificateEnabled));
            }
        }

        public bool RequestCertificateEnabled => 
            this.SelectedPublicKey != null &&
            !string.IsNullOrEmpty(this.CN) &&
            !string.IsNullOrEmpty(this.C) &&
            !string.IsNullOrEmpty(this.S) &&
            !string.IsNullOrEmpty(this.SerialNumber);

        public RelayCommand RequestCertificateCommand => new RelayCommand(this.RequestCertificate);

        public RelayCommand CancelCommand => new RelayCommand(() => this.Close(this, EventArgs.Empty));

        #region IDialogViewModel implementation 
        public event EventHandler Close;

        public event EventHandler<CertificateApplicationDialogResult> SetDialogResultAndClose;

        public CertificateApplicationDialogResult DialogResult { get; private set; }
        #endregion

        private void RequestCertificate()
        {
            var savedCertificatePath = this.fileDialogService.SaveFileDialog("req", "Certificate Request|*.req|All files|*.*");

            if (string.IsNullOrEmpty(savedCertificatePath)) return;

            var point = this.SelectedPublicKey;
            var key = this.walletController.GetKey(point);
            var publicKey = point.EncodePoint(false).Skip(1).ToArray();

            byte[] privateKey;
            using (key.Decrypt())
            {
                const int ECDSA_PRIVATE_P256_MAGIC = 0x32534345;
                privateKey = BitConverter.GetBytes(ECDSA_PRIVATE_P256_MAGIC).Concat(BitConverter.GetBytes(32)).Concat(publicKey).Concat(key.PrivateKey).ToArray();
            }

            var x509Key = new CX509PrivateKey();

            // Set property using Reflection so this project can compile if this property isn't available
            var property = typeof(CX509PrivateKey).GetProperty("AlgorithmName");

            if (property == null)
            {
                // TODO Find a way to generate a certificate without setting this property
            }
            else
            {
                property.SetValue(x509Key, "ECDSA_P256", null);
            }

            x509Key.Import("ECCPRIVATEBLOB", Convert.ToBase64String(privateKey));

            Array.Clear(privateKey, 0, privateKey.Length);

            var request = new CX509CertificateRequestPkcs10();

            request.InitializeFromPrivateKey(X509CertificateEnrollmentContext.ContextUser, x509Key, null);
            request.Subject = new CX500DistinguishedName();
            request.Subject.Encode($"CN={this.CN},C={this.C},S={this.S},SERIALNUMBER={this.SerialNumber}");
            request.Encode();

            var certificateText = "-----BEGIN NEW CERTIFICATE REQUEST-----\r\n" + request.RawData + "-----END NEW CERTIFICATE REQUEST-----\r\n";
            var certificateBytes = Encoding.UTF8.GetBytes(certificateText);

            this.fileManager.WriteAllBytes(savedCertificatePath, certificateBytes);

            this.Close(this, EventArgs.Empty);
        }
    }
}