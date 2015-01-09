﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security;
using System.Management.Automation;
using Microsoft.Azure.Commands.KeyVault.Models;
using Microsoft.Azure.Commands.KeyVault.Properties;
using Microsoft.Azure.Commands.KeyVault.WebKey;


namespace Microsoft.Azure.Commands.KeyVault
{
    /// <summary>
    /// Create a new key in key vault. This cmdlet supports the following types of 
    /// key creation.
    /// 1. Create a new HSM or software key with default key attributes
    /// 2. Create a new HSM or software key with given key attributes 
    /// 3. Create a HSM or software key by importing key material with default key 
    /// attributes
    /// 4 .Create a HSM or software key by importing key material with given key 
    /// attributes
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "AzureKeyVaultKey",
        DefaultParameterSetName = CreateParameterSet)]
    [OutputType(typeof(KeyBundle))]
    public class AddAzureKeyVaultKey : KeyVaultCmdletBase
    {

        #region Parameter Set Names

        private const string CreateParameterSet = "Create";
        private const string ImportParameterSet = "Import";

        #endregion

        #region Input Parameter Definitions

        /// <summary>
        /// Vault name
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = CreateParameterSet,
            Position = 0,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Vault name. Cmdlet constructs the FQDN of a vault based on the name and currently selected environment.")]
        [Parameter(Mandatory = true,
            ParameterSetName = ImportParameterSet,
            Position = 0,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Vault name. Cmdlet constructs the FQDN of a vault based on the name and currently selected environment.")]
        [ValidateNotNullOrEmpty]
        public string VaultName { get; set; }

        /// <summary>
        /// key name
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = CreateParameterSet,
            Position = 1,
            HelpMessage = "key name. Cmdlet constructs the FQDN of a key from vault name, currently selected environment and key name.")]
        [Parameter(Mandatory = true,
            ParameterSetName = ImportParameterSet,
            Position = 1,
            HelpMessage = "key name. Cmdlet constructs the FQDN of a key from vault name, currently selected environment and key name.")]
        [ValidateNotNullOrEmpty]
        [Alias("KeyName")]
        public string Name { get; set; }

        /// <summary>
        /// Path to the local file containing to-be-imported key material.
        /// The supported suffix are:
        /// 1. byok
        /// 2. pfx
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = ImportParameterSet,
            HelpMessage = "Path to the local file containing to-be-imported key material")]
        [ValidateNotNullOrEmpty]
        public string KeyFilePath { get; set; }

        /// <summary>
        /// Password of the imported file. 
        /// Required for pfx file
        /// </summary>
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            HelpMessage = "Password of the imported key file")]
        [ValidateNotNullOrEmpty]
        public SecureString KeyFilePassword { get; set; }

        /// <summary>
        /// Destination of the key
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = CreateParameterSet,
            HelpMessage = "Destination of the key")]
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            HelpMessage = "Destination of the key")]
        [ValidateSetAttribute(new string[] { HsmDestination, SoftwareDestination })]
        public string Destination { get; set; }

        /// <summary>
        /// Set key in disabled state if present       
        /// </summary>
        [Parameter(Mandatory = false,
            ParameterSetName = CreateParameterSet,
            HelpMessage = "Set key in disabled state if present. If not present, key is enabled.")]
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            HelpMessage = "Set key in disabled state if present. If not present, key is enabled.")]
        public SwitchParameter Disable { get; set; }

        /// <summary>
        /// Key operations 
        /// </summary>
        [Parameter(Mandatory = false,
            ParameterSetName = CreateParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The operations that can be performed with the key. If not present, all operations can be performed.")]
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The operations that can be performed with the key. If not present, all operations can be performed.")]
        public string[] KeyOps { get; set; }

        /// <summary>
        /// Key expires time in UTC time
        /// </summary>
        [Parameter(Mandatory = false,
            ParameterSetName = CreateParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The expiration time of a key in UTC time. If not present, key will not expire.")]
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The expiration time of a key in UTC time. If not present, key will not expire.")]
        public DateTime? Expires { get; set; }

        /// <summary>
        /// The UTC time before which key can't be used 
        /// </summary>
        [Parameter(Mandatory = false,
            ParameterSetName = CreateParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The UTC time before which key can't be used. If not present, no limitation.")]
        [Parameter(Mandatory = false,
            ParameterSetName = ImportParameterSet,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The UTC time before which key can't be used. If not present, no limitation.")]
        public DateTime? NotBefore { get; set; }

        #endregion

        public override void ExecuteCmdlet()
        {
            try
            {
                KeyBundle keyBundle;
                switch (ParameterSetName)
                {
                    case CreateParameterSet:
                        keyBundle = this.DataServiceClient.CreateKey(
                            VaultName,
                            Name,
                            CreateKeyAttributes());
                        break;

                    case ImportParameterSet:
                        bool? importToHsm = null;
                        keyBundle = this.DataServiceClient.ImportKey(
                            VaultName, Name,
                            CreateKeyAttributes(),
                            CreateWebKeyFromFile(),
                            string.IsNullOrEmpty(Destination) ? importToHsm : HsmDestination.Equals(Destination, StringComparison.OrdinalIgnoreCase));
                        break;

                    default:
                        throw new ArgumentException(Resources.BadParameterSetName);
                }

                this.WriteObject(keyBundle);
            }
            catch (Exception ex)
            {
                this.WriteErrorDetails(ex);
            }
        }

        internal KeyAttributes CreateKeyAttributes()
        {
            string keyType = string.Empty;

            if (!string.IsNullOrEmpty(Destination))
            {
                keyType = (HsmDestination.Equals(Destination, StringComparison.OrdinalIgnoreCase)) ? JsonWebKeyType.RsaHsm : JsonWebKeyType.Rsa;
            }

            return new KeyAttributes(
                !Disable.IsPresent,
                Expires,
                NotBefore,
                keyType,
                KeyOps);
        }

        internal JsonWebKey CreateWebKeyFromFile()
        {
            FileInfo keyFile = new FileInfo(this.GetUnresolvedProviderPathFromPSPath(this.KeyFilePath));
            if (!keyFile.Exists)
            {
                throw new FileNotFoundException(string.Format(Resources.KeyFileNotFound, this.KeyFilePath));
            }

            var converterChain = WebKeyConverterFactory.CreateConverterChain();
            return converterChain.ConvertKeyFromFile(keyFile, KeyFilePassword);
        }

        private const string HsmDestination = "HSM";
        private const string SoftwareDestination = "Software";
    }
}