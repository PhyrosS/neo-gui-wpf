﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Neo.Core;
using Neo.IO.Json;
using Neo.SmartContract;

using Neo.Gui.Base.Controllers;
using Neo.Gui.Base.Data;
using Neo.Gui.Base.Dialogs.Interfaces;
using Neo.Gui.Base.Dialogs.Results;
using Neo.Gui.Base.Globalization;
using Neo.Gui.Base.Services;
using Neo.Gui.Base.MVVM;
using Neo.Gui.Base.Dialogs.Results.Wallets;
using Neo.Gui.Base.Managers;

using Neo.Gui.Wpf.MVVM;

namespace Neo.Gui.Wpf.Views.Wallets
{
    public class TradeViewModel : ViewModelBase, IDialogViewModel<TradeDialogResult>
    {
        private readonly IDialogManager dialogManager;
        private readonly IWalletController walletController;
        private readonly IDispatchService dispatchService;

        private string payToAddress;
        private string myRequest;
        private string counterPartyRequest;
        
        private bool mergeEnabled;

        private UInt160 scriptHash;

        private int selectedTabIndex;

        public TradeViewModel(
            IDialogManager dialogManager,
            IWalletController walletController,
            IDispatchService dispatchService)
        {
            this.dialogManager = dialogManager;
            this.walletController = walletController;
            this.dispatchService = dispatchService;

            this.Items = new ObservableCollection<TransactionOutputItem>();
        }

        public ObservableCollection<TransactionOutputItem> Items { get; }

        public string PayToAddress
        {
            get => this.payToAddress;
            set
            {
                if (this.payToAddress == value) return;

                this.payToAddress = value;

                NotifyPropertyChanged();

                // Update dependent properties
                NotifyPropertyChanged(nameof(this.ScriptHash));
                
                try
                {
                    this.ScriptHash = this.walletController.ToScriptHash(this.PayToAddress);
                }
                catch (FormatException)
                {
                    this.ScriptHash = null;
                }

                this.dispatchService.InvokeOnMainUIThread(() => this.Items.Clear());
            }
        }

        public UInt160 ScriptHash
        {
            get => this.scriptHash;
            set
            {
                if (this.scriptHash == value) return;

                this.scriptHash = value;

                NotifyPropertyChanged();

                // Update dependent property
                NotifyPropertyChanged(nameof(this.ItemsListEnabled));
            }
        }

        public bool ItemsListEnabled => this.ScriptHash != null;

        public string MyRequest
        {
            get => this.myRequest;
            set
            {
                if (this.myRequest == value) return;

                this.myRequest = value;

                NotifyPropertyChanged();
            }
        }

        public string CounterPartyRequest
        {
            get => this.counterPartyRequest;
            set
            {
                if (this.counterPartyRequest == value) return;

                this.counterPartyRequest = value;

                NotifyPropertyChanged();

                // Update dependent property
                NotifyPropertyChanged(nameof(this.ValidateEnabled));
            }
        }

        public bool InitiateEnabled => this.Items.Count > 0;

        public bool ValidateEnabled => !string.IsNullOrEmpty(this.CounterPartyRequest) && !string.IsNullOrEmpty(this.MyRequest);

        public bool MergeEnabled
        {
            get => this.mergeEnabled;
            set
            {
                if (this.mergeEnabled == value) return;

                this.mergeEnabled = value;

                NotifyPropertyChanged();
            }
        }

        public int SelectedTabIndex
        {
            get => this.selectedTabIndex;
            set
            {
                if (this.selectedTabIndex == value) return;

                this.selectedTabIndex = value;

                NotifyPropertyChanged();
            }
        }

        public RelayCommand InitiateCommand => new RelayCommand(this.Initiate);

        public RelayCommand ValidateCommand => new RelayCommand(this.Validate);

        public RelayCommand MergeCommand => new RelayCommand(this.Merge);

        #region IDialogViewModel implementation 
        public event EventHandler Close;

        public event EventHandler<TradeDialogResult> SetDialogResultAndClose;

        public TradeDialogResult DialogResult { get; private set; }
        #endregion

        public void UpdateInitiateButtonEnabled()
        {
            NotifyPropertyChanged(nameof(this.InitiateEnabled));
        }

        private void Initiate()
        {
            var txOutputs = this.Items.Select(p => p.ToTxOutput());

            var tx = this.walletController.MakeTransaction(new ContractTransaction
            {
                Outputs = txOutputs.ToArray()
            }, fee: Fixed8.Zero);

            this.MyRequest = RequestToJson(tx).ToString();

            this.dialogManager.ShowInformationDialog(Strings.TradeRequestCreatedCaption, Strings.TradeRequestCreatedMessage, this.MyRequest);
            
            this.SelectedTabIndex = 1;
        }

        private async void Validate()
        {
            IEnumerable<CoinReference> inputs;
            IEnumerable<TransactionOutput> outputs;
            var json = JObject.Parse(this.CounterPartyRequest);
            if (json.ContainsProperty("hex"))
            {
                var txMine = this.JsonToRequest(JObject.Parse(this.MyRequest));
                var txOthers = (ContractTransaction)ContractParametersContext.FromJson(json).Verifiable;
                inputs = txOthers.Inputs.Except(txMine.Inputs);
                var outputsOthers = new List<TransactionOutput>(txOthers.Outputs);
                foreach (var outputMine in txMine.Outputs)
                {
                    var outputOthers = outputsOthers.FirstOrDefault(p => p.AssetId == outputMine.AssetId && p.Value == outputMine.Value && p.ScriptHash == outputMine.ScriptHash);
                    if (outputOthers == null)
                    {
                        this.dialogManager.ShowMessageDialog(Strings.Failed, Strings.TradeFailedFakeDataMessage);
                        return;
                    }
                    outputsOthers.Remove(outputOthers);
                }
                outputs = outputsOthers;
            }
            else
            {
                var txOthers = this.JsonToRequest(json);
                inputs = txOthers.Inputs;
                outputs = txOthers.Outputs;
            }

            try
            {
                if (inputs.Select(p => this.walletController.GetTransaction(p.PrevHash).Outputs[p.PrevIndex].ScriptHash).Distinct().Any(p => this.walletController.WalletContainsAddress(p)))
                {
                    this.dialogManager.ShowMessageDialog(Strings.Failed, Strings.TradeFailedInvalidDataMessage);
                    return;
                }
            }
            catch
            {
                this.dialogManager.ShowMessageDialog(Strings.Failed, Strings.TradeFailedNoSyncMessage);
                return;
            }

            outputs = outputs.Where(p => this.walletController.WalletContainsAddress(p.ScriptHash));

            var dialogResult = this.dialogManager.ShowDialog<TradeVerificationDialogResult, TradeVerificationLoadParameters>(
                new LoadParameters<TradeVerificationLoadParameters>(new TradeVerificationLoadParameters(outputs)));

            this.MergeEnabled = dialogResult.TradeAccepted;
        }

        private void Merge()
        {
            ContractParametersContext context;
            var json1 = JObject.Parse(this.CounterPartyRequest);
            if (json1.ContainsProperty("hex"))
            {
                context = ContractParametersContext.FromJson(json1);
            }
            else
            {
                var tx1 = this.JsonToRequest(json1);
                var tx2 = this.JsonToRequest(JObject.Parse(this.MyRequest));
                context = new ContractParametersContext(new ContractTransaction
                {
                    Attributes = new TransactionAttribute[0],
                    Inputs = tx1.Inputs.Concat(tx2.Inputs).ToArray(),
                    Outputs = tx1.Outputs.Concat(tx2.Outputs).ToArray()
                });
            }

            this.walletController.Sign(context);

            if (context.Completed)
            {
                context.Verifiable.Scripts = context.GetScripts();

                var tx = (ContractTransaction)context.Verifiable;
                this.walletController.Relay(tx);

                this.dialogManager.ShowInformationDialog(Strings.TradeSuccessCaption, Strings.TradeSuccessMessage, tx.Hash.ToString());
            }
            else
            {
                this.dialogManager.ShowInformationDialog(Strings.TradeNeedSignatureCaption, Strings.TradeNeedSignatureMessage, context.ToString());
            }
        }

        private ContractTransaction JsonToRequest(JObject json)
        {
            return new ContractTransaction
            {
                Inputs = ((JArray)json["vin"]).Select(p => new CoinReference
                {
                    PrevHash = UInt256.Parse(p["txid"].AsString()),
                    PrevIndex = (ushort)p["vout"].AsNumber()
                }).ToArray(),
                Outputs = ((JArray)json["vout"]).Select(p => new TransactionOutput
                {
                    AssetId = UInt256.Parse(p["asset"].AsString()),
                    Value = Fixed8.Parse(p["value"].AsString()),
                    ScriptHash = this.walletController.ToScriptHash(p["address"].AsString())
                }).ToArray()
            };
        }

        private JObject RequestToJson(ContractTransaction tx)
        {
            var json = new JObject
            {
                ["vin"] = tx.Inputs.Select(p => p.ToJson()).ToArray(),
                ["vout"] = tx.Outputs.Select((p, i) => p.ToJson((ushort)i)).ToArray(),
                ["change_address"] = this.walletController.ToAddress(this.walletController.GetChangeAddress())
            };
            return json;
        }
    }
}