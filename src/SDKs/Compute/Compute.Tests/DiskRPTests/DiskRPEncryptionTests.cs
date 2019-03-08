﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest.Azure;
using Microsoft.Rest.ClientRuntime.Azure.TestFramework;
using Xunit;

namespace Compute.Tests.DiskRPTests
{
    public class DiskRPEncryptionTests : DiskRPTestsBase
    {
        private static string DiskRPLocation = "westcentralus";

        /// <summary>
        /// positive test for testing disks encryption
        /// to enable this test, replace [Fact(Skip = "skipping positive test")] with [Fact]
        /// a valid keyvault is needed for this test
        /// encrypted disk will be retrievable through the encryptionkeyuri
        /// </summary>
        [Fact]
        public void DiskEncryptionPositiveTest()
        {
            using (MockContext context = MockContext.Start(this.GetType().FullName))
            {
                EnsureClientsInitialized(context);
                string testVaultId = @"/subscriptions/" + m_CrpClient.SubscriptionId + "/resourceGroups/24/providers/Microsoft.KeyVault/vaults/swagger";
                string encryptionKeyUri = @"https://swagger.vault.azure.net/keys/swagger-key/9b47c354b4fe4f24a1059a97ad1c5727";
                string secretUri = @"https://swagger.vault.azure.net/secrets/swagger-secret/95112a1a98c04e7eaba960265fc54897";

                var rgName = TestUtilities.GenerateName(TestPrefix);
                var diskName = TestUtilities.GenerateName(DiskNamePrefix);
                Disk disk = GenerateDefaultDisk(DiskCreateOption.Empty, rgName, 10);
                disk.EncryptionSettingsCollection = GetDiskEncryptionSettings(testVaultId, encryptionKeyUri, secretUri);
                disk.Location = DiskRPLocation;

                try
                {
                    m_ResourcesClient.ResourceGroups.CreateOrUpdate(rgName, new ResourceGroup { Location = DiskRPLocation });
                    //put disk
                    m_CrpClient.Disks.CreateOrUpdate(rgName, diskName, disk);
                    Disk diskOut = m_CrpClient.Disks.Get(rgName, diskName);

                    Validate(disk, diskOut, disk.Location);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SecretUrl, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SecretUrl);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SourceVault.Id, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SourceVault.Id);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.KeyUrl, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.KeyUrl);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.SourceVault.Id, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.SourceVault.Id);
                    m_CrpClient.Disks.Delete(rgName, diskName);
                }
                finally
                {
                    m_ResourcesClient.ResourceGroups.Delete(rgName);
                }
            }
        }

        [Fact]
        public void DiskEncryptionNegativeTest()
        {
            using (MockContext context = MockContext.Start(this.GetType().FullName))
            {
                EnsureClientsInitialized(context);
                string fakeTestVaultId =
                @"/subscriptions/" + m_CrpClient.SubscriptionId + "/resourceGroups/testrg/providers/Microsoft.KeyVault/vaults/keyvault";
                string fakeEncryptionKeyUri = @"https://testvault.vault.azure.net/secrets/swaggersecret/test123";

                var rgName = TestUtilities.GenerateName(TestPrefix);
                var diskName = TestUtilities.GenerateName(DiskNamePrefix);
                Disk disk = GenerateDefaultDisk(DiskCreateOption.Empty, rgName, 10);
                disk.EncryptionSettingsCollection = GetDiskEncryptionSettings(fakeTestVaultId, fakeEncryptionKeyUri, fakeEncryptionKeyUri);
                disk.Location = DiskRPLocation;

                try
                {
                    m_ResourcesClient.ResourceGroups.CreateOrUpdate(rgName,
                        new ResourceGroup {Location = DiskRPLocation});

                    m_CrpClient.Disks.CreateOrUpdate(rgName, diskName, disk);
                    Disk diskOut = m_CrpClient.Disks.Get(rgName, diskName);

                    Validate(disk, diskOut, disk.Location);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SecretUrl, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SecretUrl);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SourceVault.Id, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().DiskEncryptionKey.SourceVault.Id);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.KeyUrl, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.KeyUrl);
                    Assert.Equal(disk.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.SourceVault.Id, diskOut.EncryptionSettingsCollection.EncryptionSettings.First().KeyEncryptionKey.SourceVault.Id);
                    m_CrpClient.Disks.Delete(rgName, diskName);
                }
                catch(CloudException ex)
                {
                    string coreresponsestring = fakeEncryptionKeyUri +
                        " is not a valid versioned Key Vault Secret URL. It should be in the format https://<vaultEndpoint>/secrets/<secretName>/<secretVersion>.";
                    Assert.Equal(coreresponsestring, ex.Message);
                    Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest);
                }
                finally
                {
                    m_ResourcesClient.ResourceGroups.Delete(rgName);
                }
            }
        }

        private EncryptionSettingsCollection GetDiskEncryptionSettings(string testVaultId, string encryptionKeyUri, string secretUri, bool setEnabled = true)
        {
            EncryptionSettingsCollection diskEncryptionSettings = new EncryptionSettingsCollection
            {
                Enabled = true,
                EncryptionSettings = new List<EncryptionSettingsElement>
                {
                    new EncryptionSettingsElement
                    {
                        DiskEncryptionKey = new KeyVaultAndSecretReference
                        {
                            SecretUrl = secretUri,
                            SourceVault = new SourceVault
                            {
                                Id = testVaultId
                            }
                        },
                        KeyEncryptionKey = new KeyVaultAndKeyReference
                        {
                            KeyUrl = encryptionKeyUri,
                            SourceVault = new SourceVault
                            {
                                Id = testVaultId
                            }
                        }
                    }
                }
            };
            return diskEncryptionSettings;
        }
    }
}
