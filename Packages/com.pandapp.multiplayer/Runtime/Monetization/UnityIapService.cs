#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace Pandapp.Multiplayer.Monetization
{
    public sealed class UnityIapService : MonoBehaviour, IIapService, IIapReceiptService, IIapProductMetadataService, IDetailedStoreListener
    {
        private readonly Dictionary<string, Action<IapPurchaseResult>> pendingPurchases =
            new Dictionary<string, Action<IapPurchaseResult>>();
        private readonly HashSet<string> receiptProductIds = new HashSet<string>();

        private MonetizationSettings settings;
        private IStoreController controller;
        private IExtensionProvider extensions;
        private bool initializationRequested;

        public IapProvider Provider => IapProvider.UnityIap;
        public bool IsInitialized => controller != null && extensions != null;

        public void Initialize(MonetizationSettings monetizationSettings)
        {
            settings = monetizationSettings;
            if (IsInitialized || initializationRequested)
            {
                return;
            }

            if (settings == null)
            {
                Debug.LogError("[UnityIapService] MonetizationSettings not assigned.");
                return;
            }

            if (!HasConfiguredProducts(settings))
            {
                Debug.LogWarning("[UnityIapService] No IAP product ids configured.");
                return;
            }

            initializationRequested = true;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            var addedProductIds = new HashSet<string>();
            if (settings.products != null)
            {
                for (var i = 0; i < settings.products.Length; i++)
                {
                    var product = settings.products[i];
                    var productId = product.productId;
                    if (string.IsNullOrWhiteSpace(productId))
                    {
                        continue;
                    }

                    productId = productId.Trim();
                    if (!addedProductIds.Add(productId))
                    {
                        continue;
                    }

                    builder.AddProduct(productId, product.productType);
                }
            }

            if (settings.productIds != null)
            {
                for (var i = 0; i < settings.productIds.Length; i++)
                {
                    var productId = settings.productIds[i];
                    if (string.IsNullOrWhiteSpace(productId))
                    {
                        continue;
                    }

                    productId = productId.Trim();
                    if (!addedProductIds.Add(productId))
                    {
                        continue;
                    }

                    builder.AddProduct(productId, ResolveProductType(productId));
                }
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void Purchase(string productId, Action<IapPurchaseResult> callback)
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(productId))
            {
                callback?.Invoke(IapPurchaseResult.Failed);
                return;
            }

            var product = controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                callback?.Invoke(IapPurchaseResult.Failed);
                return;
            }

            pendingPurchases[product.definition.id] = callback;
            controller.InitiatePurchase(product);
        }

        public void RestorePurchases(Action<bool> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(false);
                return;
            }

#if UNITY_IOS || UNITY_STANDALONE_OSX
            extensions.GetExtension<IAppleExtensions>().RestoreTransactions((success, _) =>
            {
                CacheCurrentReceipts();
                callback?.Invoke(success);
            });
#elif UNITY_ANDROID
            extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions((success, _) =>
            {
                CacheCurrentReceipts();
                callback?.Invoke(success);
            });
#else
            CacheCurrentReceipts();
            callback?.Invoke(true);
#endif
        }

        public bool HasReceipt(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            if (receiptProductIds.Contains(productId))
            {
                return true;
            }

            var product = controller?.products.WithID(productId);
            return product != null && product.hasReceipt;
        }

        public bool TryGetLocalizedPriceString(string productId, out string price)
        {
            price = string.Empty;
            if (string.IsNullOrWhiteSpace(productId) || controller == null)
            {
                return false;
            }

            var product = controller.products.WithID(productId);
            var localizedPrice = product?.metadata?.localizedPriceString;
            if (string.IsNullOrWhiteSpace(localizedPrice))
            {
                return false;
            }

            price = localizedPrice;
            return true;
        }

        public void OnInitialized(IStoreController storeController, IExtensionProvider extensionProvider)
        {
            controller = storeController;
            extensions = extensionProvider;
            initializationRequested = false;
            CacheCurrentReceipts();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            controller = null;
            extensions = null;
            initializationRequested = false;
            Debug.LogWarning($"[UnityIapService] Init failed: {error} {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args?.purchasedProduct;
            var productId = product?.definition?.id;
            if (string.IsNullOrWhiteSpace(productId))
            {
                return PurchaseProcessingResult.Complete;
            }

            if (product.hasReceipt)
            {
                receiptProductIds.Add(productId);
            }

            if (pendingPurchases.TryGetValue(productId, out var callback))
            {
                pendingPurchases.Remove(productId);
                callback?.Invoke(IapPurchaseResult.Purchased);
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            DispatchPurchaseFailure(product, failureReason);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            DispatchPurchaseFailure(product, failureDescription?.reason ?? PurchaseFailureReason.Unknown);
        }

        private void DispatchPurchaseFailure(Product product, PurchaseFailureReason failureReason)
        {
            var productId = product?.definition?.id;
            if (string.IsNullOrWhiteSpace(productId)
                || !pendingPurchases.TryGetValue(productId, out var callback))
            {
                return;
            }

            pendingPurchases.Remove(productId);
            callback?.Invoke(failureReason == PurchaseFailureReason.UserCancelled
                ? IapPurchaseResult.Cancelled
                : IapPurchaseResult.Failed);
        }

        private void CacheCurrentReceipts()
        {
            receiptProductIds.Clear();
            var products = controller?.products?.all;
            if (products == null)
            {
                return;
            }

            for (var i = 0; i < products.Length; i++)
            {
                var product = products[i];
                if (product?.definition == null || !product.hasReceipt)
                {
                    continue;
                }

                receiptProductIds.Add(product.definition.id);
            }
        }

        private static ProductType ResolveProductType(string productId)
        {
            return productId.IndexOf("no_ads", StringComparison.OrdinalIgnoreCase) >= 0
                ? ProductType.NonConsumable
                : ProductType.Consumable;
        }

        private static bool HasConfiguredProducts(MonetizationSettings monetizationSettings)
        {
            return HasProductDefinitions(monetizationSettings.products)
                || HasProductIds(monetizationSettings.productIds);
        }

        private static bool HasProductDefinitions(IapProductDefinition[] products)
        {
            if (products == null)
            {
                return false;
            }

            for (var i = 0; i < products.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(products[i].productId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasProductIds(string[] productIds)
        {
            if (productIds == null)
            {
                return false;
            }

            for (var i = 0; i < productIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(productIds[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
