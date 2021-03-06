﻿using System;
using System.Collections.Generic;
using System.Text;
using Neo.Core;
using Neo.Gui.Base.Data;

namespace Neo.Gui.ViewModels.Tests.Builders
{
    public class AssetItemBuilder
    {
        private string internalName = "Name";
        private string internalType = "Type";
        private string internalValue = "Value";
        private string internalIssuer = "Issuer";
        private AssetState internalAssetState = new AssetState { Owner = new Cryptography.ECC.ECPoint() };

        public AssetItemBuilder WithName(string name)
        {
            this.internalName = name;
            return this;
        }

        public AssetItemBuilder WithType(string type)
        {
            this.internalType = type;
            return this;
        }

        public AssetItemBuilder WithValue(string value)
        {
            this.internalValue = value;
            return this;
        }

        public AssetItemBuilder WithIssuer(string issuer)
        {
            this.internalIssuer = issuer;
            return this;
        }

        public AssetItemBuilder WithState(AssetState assetState)
        {
            this.internalAssetState = assetState;
            return this;
        }

        public AssetItem Build()
        {
            return new AssetItem
            {
                Name = this.internalName,
                Type = this.internalType, 
                Value = this.internalValue,
                Issuer = this.internalIssuer,
                State = this.internalAssetState
            };
        }
    }
}
