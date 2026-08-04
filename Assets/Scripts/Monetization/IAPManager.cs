using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    /// <summary>
    /// Defines a purchasable product within the game's store.
    /// </summary>
    [Serializable]
    public struct ProductDefinition
    {
        /// <summary>The store-specific product identifier (e.g., "com.hermes.gems_pack_1").</summary>
        public string Id;

        /// <summary>Human-readable product name for UI display.</summary>
        public string DisplayName;

        /// <summary>Short description of what the product contains.</summary>
        public string Description;

        /// <summary>Consumable (can be purchased repeatedly) vs. NonConsumable (purchased once).</summary>
        public ProductType Type;

        /// <summary>Price in USD (for preview purposes; actual price is set in App Store/Google Play).</summary>
        public decimal PriceUsd;

        /// <summary>Currency code (e.g., "USD", "EUR").</summary>
        public string IsoCurrencyCode;
    }

    /// <summary>
    /// Product type classification for Unity IAP.
    /// </summary>
    public enum ProductType
    {
        /// <summary>Can be purchased repeatedly (e.g., coins, gems, energy).</summary>
        Consumable,
        /// <summary>Purchased once and permanently unlocked (e.g., remove ads, full game).</summary>
        NonConsumable,
        /// <summary>Auto-renewable subscription.</summary>
        Subscription
    }

    /// <summary>
    /// Result returned from a purchase operation.
    /// </summary>
    public struct PurchaseResult
    {
        /// <summary>Whether the purchase succeeded.</summary>
        public bool Success;

        /// <summary>The product identifier that was purchased.</summary>
        public string ProductId;

        /// <summary>If the purchase failed, a human-readable error message.</summary>
        public string ErrorMessage;

        /// <summary>A unique transaction identifier (receipt), or empty on failure.</summary>
        public string TransactionId;

        /// <summary>Creates a successful purchase result.</summary>
        public static PurchaseResult Succeeded(string productId, string transactionId) =>
            new PurchaseResult { Success = true, ProductId = productId, TransactionId = transactionId, ErrorMessage = null };

        /// <summary>Creates a failed purchase result.</summary>
        public static PurchaseResult Failed(string productId, string error) =>
            new PurchaseResult { Success = false, ProductId = productId, TransactionId = null, ErrorMessage = error };
    }

    /// <summary>
    /// Unity IAP (In-App Purchase) wrapper.
    /// Provides a clean singleton interface for product registration, purchasing, and receipt validation.
    ///
    /// To use, install the "In App Purchasing" package from Unity Package Manager.
    /// Configure products in the Unity IAP Catalog or call <see cref="RegisterProduct"/> at runtime.
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static IAPManager _instance;

        /// <summary>
        /// Gets the singleton instance of IAPManager.
        /// </summary>
        public static IAPManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[IAPManager]");
                    _instance = go.AddComponent<IAPManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the IAP store has been initialized successfully.</summary>
        public event Action OnStoreInitialized;

        /// <summary>Fired when store initialization fails. Parameter: error message.</summary>
        public event Action<string> OnStoreInitializationFailed;

        /// <summary>Fired when a purchase completes successfully.</summary>
        public event Action<PurchaseResult> OnPurchaseComplete;

        /// <summary>Fired when a purchase fails or is cancelled by the user.</summary>
        public event Action<PurchaseResult> OnPurchaseFailed;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        /// <summary>
        /// Whether the IAP store has been fully initialized and is ready for transactions.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// The list of products registered for sale. Populated after initialization.
        /// </summary>
        public IReadOnlyList<ProductDefinition> RegisteredProducts => _products.AsReadOnly();

        private readonly List<ProductDefinition> _products = new List<ProductDefinition>();

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[IAPManager] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Register default products
            RegisterDefaultProducts();
        }

        private void Start()
        {
            InitializeStore();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Product registration
        // ------------------------------------------------------------------

        /// <summary>
        /// Registers a product with the IAP system.
        /// Must be called before <see cref="InitializeStore"/>.
        /// </summary>
        /// <param name="product">The product definition to register.</param>
        public void RegisterProduct(ProductDefinition product)
        {
            if (string.IsNullOrEmpty(product.Id))
            {
                Debug.LogError("[IAPManager] Cannot register product: Id is null or empty.");
                return;
            }

            _products.Add(product);
            Debug.Log($"[IAPManager] Registered product: '{product.Id}' ({product.Type})");
        }

        /// <summary>
        /// Removes a previously registered product.
        /// </summary>
        /// <param name="productId">The product identifier to remove.</param>
        /// <returns>True if the product was found and removed.</returns>
        public bool UnregisterProduct(string productId)
        {
            int removed = _products.RemoveAll(p => p.Id == productId);
            if (removed > 0)
            {
                Debug.Log($"[IAPManager] Unregistered product: '{productId}'.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Populates the product list with sensible defaults for a typical F2P mobile game.
        /// Override or extend via <see cref="RegisterProduct"/> before calling <see cref="InitializeStore"/>.
        /// </summary>
        private void RegisterDefaultProducts()
        {
            RegisterProduct(new ProductDefinition
            {
                Id = "com.hermes.coins_100",
                DisplayName = "100 Coins",
                Description = "A small pouch of in-game coins.",
                Type = ProductType.Consumable,
                PriceUsd = 0.99m,
                IsoCurrencyCode = "USD"
            });

            RegisterProduct(new ProductDefinition
            {
                Id = "com.hermes.coins_500",
                DisplayName = "500 Coins",
                Description = "A hefty bag of in-game coins.",
                Type = ProductType.Consumable,
                PriceUsd = 3.99m,
                IsoCurrencyCode = "USD"
            });

            RegisterProduct(new ProductDefinition
            {
                Id = "com.hermes.remove_ads",
                DisplayName = "Remove Ads",
                Description = "Permanently removes all advertisements.",
                Type = ProductType.NonConsumable,
                PriceUsd = 2.99m,
                IsoCurrencyCode = "USD"
            });
        }

        // ------------------------------------------------------------------
        // Store initialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes the Unity IAP store.
        /// In the Unity Editor, simulates success after a short delay.
        /// On device, uses UnityPurchasing.Initialize().
        /// </summary>
        public void InitializeStore()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[IAPManager] Store already initialized.");
                return;
            }

            Debug.Log("[IAPManager] Initializing Unity IAP store...");

#if UNITY_EDITOR
            Debug.Log("[IAPManager] Editor mode: simulating store initialization.");
            StartCoroutine(SimulateEditorInit());
#else
            // Real Unity IAP initialization — requires Unity Purchasing package.
            // var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            // foreach (var product in _products)
            // {
            //     builder.AddProduct(product.Id, ConvertProductType(product.Type));
            // }
            // UnityPurchasing.Initialize(new IAPListener(this), builder);

            StartCoroutine(SimulateEditorInit());
#endif
        }

        private System.Collections.IEnumerator SimulateEditorInit()
        {
            yield return new WaitForSeconds(0.3f);
            IsInitialized = true;
            OnStoreInitialized?.Invoke();
            Debug.Log("[IAPManager] Store initialized (simulated).");
        }

        // ------------------------------------------------------------------
        // Purchasing
        // ------------------------------------------------------------------

        /// <summary>
        /// Initiates a purchase for the specified product.
        /// </summary>
        /// <param name="productId">The product identifier to purchase.</param>
        /// <returns>True if the purchase request was initiated; false if the store is not initialized or the product is unknown.</returns>
        public bool PurchaseProduct(string productId)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[IAPManager] Cannot purchase: store not initialized.");
                OnPurchaseFailed?.Invoke(PurchaseResult.Failed(productId, "Store not initialized."));
                return false;
            }

            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogError("[IAPManager] Cannot purchase: productId is null or empty.");
                OnPurchaseFailed?.Invoke(PurchaseResult.Failed(productId, "Invalid product ID."));
                return false;
            }

            // Verify product exists in our list
            ProductDefinition? productDef = _products.Find(p => p.Id == productId);
            if (productDef == null)
            {
                Debug.LogError($"[IAPManager] Cannot purchase: product '{productId}' is not registered.");
                OnPurchaseFailed?.Invoke(PurchaseResult.Failed(productId, "Product not registered."));
                return false;
            }

            Debug.Log($"[IAPManager] Initiating purchase for '{productId}'...");

#if UNITY_EDITOR
            Debug.Log("[IAPManager] Editor mode: simulating successful purchase.");
            StartCoroutine(SimulateEditorPurchase(productId));
#else
            // Real Unity IAP purchase.
            // var storeProduct = UnityPurchasing.GetProduct(productId);
            // if (storeProduct != null && storeProduct.availableToPurchase)
            // {
            //     m_StoreController.InitiatePurchase(storeProduct);
            // }
            // else
            // {
            //     OnPurchaseFailed?.Invoke(PurchaseResult.Failed(productId, "Product not available in store."));
            //     return false;
            // }
            StartCoroutine(SimulateEditorPurchase(productId));
#endif
            return true;
        }

        private System.Collections.IEnumerator SimulateEditorPurchase(string productId)
        {
            yield return new WaitForSeconds(0.5f);

            string transactionId = $"sim_{Guid.NewGuid():N}";
            var result = PurchaseResult.Succeeded(productId, transactionId);
            OnPurchaseComplete?.Invoke(result);
            Debug.Log($"[IAPManager] Purchase succeeded: '{productId}' (txn: {transactionId})");
        }

        // ------------------------------------------------------------------
        // Receipt / restore
        // ------------------------------------------------------------------

        /// <summary>
        /// Restores previously purchased non-consumable products (required for iOS).
        /// </summary>
        public void RestorePurchases()
        {
            if (!IsInitialized)
            {
                Debug.LogError("[IAPManager] Cannot restore purchases: store not initialized.");
                return;
            }

            Debug.Log("[IAPManager] Restoring purchases...");

#if UNITY_EDITOR
            Debug.Log("[IAPManager] Editor mode: purchase restoration simulated (no-op).");
#else
            // var appleModule = StandardPurchasingModule.Instance().StoreSpecificModule as AppleInAppPurchasingModule;
            // appleModule?.RestoreTransactions(null);
#endif
        }

        /// <summary>
        /// Gets the localized price string for a product (e.g., "$0.99").
        /// </summary>
        /// <param name="productId">The product identifier.</param>
        /// <returns>The price string, or "N/A" if the product is not found or store not initialized.</returns>
        public string GetLocalizedPrice(string productId)
        {
            if (!IsInitialized)
            {
                return "N/A";
            }

            ProductDefinition? def = _products.Find(p => p.Id == productId);
            if (def == null)
                return "N/A";

            return def.Value.PriceUsd.ToString("C");
        }

        /// <summary>
        /// Returns true if the specified product exists in the registered product list.
        /// </summary>
        public bool HasProduct(string productId)
        {
            return _products.Exists(p => p.Id == productId);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Converts our ProductType enum to Unity IAP's ProductType.
        /// </summary>
        /// <param name="type">The local product type.</param>
        /// <returns>The corresponding Unity IAP ProductType.</returns>
        private static UnityEngine.Purchasing.ProductType ConvertProductType(ProductType type)
        {
            switch (type)
            {
                case ProductType.Consumable:    return UnityEngine.Purchasing.ProductType.Consumable;
                case ProductType.NonConsumable: return UnityEngine.Purchasing.ProductType.NonConsumable;
                case ProductType.Subscription:  return UnityEngine.Purchasing.ProductType.Subscription;
                default:
                    Debug.LogWarning($"[IAPManager] Unknown ProductType '{type}', defaulting to Consumable.");
                    return UnityEngine.Purchasing.ProductType.Consumable;
            }
        }

        // ------------------------------------------------------------------
        // IAP listener implementation (for real device integration)
        // ------------------------------------------------------------------

        /*
        private sealed class IAPListener : IStoreListener
        {
            private readonly IAPManager _manager;

            public IAPListener(IAPManager manager) => _manager = manager;

            public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
            {
                _manager.IsInitialized = true;
                _manager.OnStoreInitialized?.Invoke();
            }

            public void OnInitializeFailed(InitializationFailureReason error)
            {
                _manager.IsInitialized = false;
                _manager.OnStoreInitializationFailed?.Invoke(error.ToString());
            }

            public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
            {
                var result = PurchaseResult.Succeeded(
                    args.purchasedProduct.definition.id,
                    args.purchasedProduct.transactionID
                );
                _manager.OnPurchaseComplete?.Invoke(result);
                return PurchaseProcessingResult.Complete;
            }

            public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
            {
                var result = PurchaseResult.Failed(
                    product.definition.id,
                    failureReason.ToString()
                );
                _manager.OnPurchaseFailed?.Invoke(result);
            }
        }
        */
    }
}
