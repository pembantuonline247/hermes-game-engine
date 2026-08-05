using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    [Serializable]
    public struct ProductDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public ProductType Type;
        public decimal PriceUsd;
        public string IsoCurrencyCode;
    }

    public enum ProductType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    public struct PurchaseResult
    {
        public bool Success;
        public string ProductId;
        public string ErrorMessage;
        public string TransactionId;

        public static PurchaseResult Succeeded(string productId, string transactionId) =>
            new PurchaseResult { Success = true, ProductId = productId, TransactionId = transactionId, ErrorMessage = null };

        public static PurchaseResult Failed(string productId, string error) =>
            new PurchaseResult { Success = false, ProductId = productId, TransactionId = null, ErrorMessage = error };
    }

    public class IAPManager : MonoBehaviour
    {
        private static IAPManager _instance;
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

        public event Action OnStoreInitialized;
        public event Action<string> OnStoreInitializationFailed;
        public event Action<PurchaseResult> OnPurchaseComplete;
        public event Action<PurchaseResult> OnPurchaseFailed;

        public bool IsInitialized { get; private set; }
        public IReadOnlyList<ProductDefinition> RegisteredProducts => _products.AsReadOnly();

        private readonly List<ProductDefinition> _products = new List<ProductDefinition>();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RegisterDefaultProducts();
        }

        private void Start() { InitializeStore(); }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public void RegisterProduct(ProductDefinition product)
        {
            if (string.IsNullOrEmpty(product.Id)) return;
            _products.Add(product);
        }

        private void RegisterDefaultProducts()
        {
            RegisterProduct(new ProductDefinition
            {
                Id = "com.pembantu.spacedodger.remove_ads",
                DisplayName = "Remove Ads",
                Description = "Permanently removes all advertisements from Space Dodger.",
                Type = ProductType.NonConsumable,
                PriceUsd = 2.99m,
                IsoCurrencyCode = "USD"
            });
            RegisterProduct(new ProductDefinition
            {
                Id = "com.pembantu.spacedodger.coins_500",
                DisplayName = "500 Coins",
                Description = "A bundle of 500 in-game coins.",
                Type = ProductType.Consumable,
                PriceUsd = 3.99m,
                IsoCurrencyCode = "USD"
            });
        }

        public void InitializeStore()
        {
            if (IsInitialized) return;
            Debug.Log("[IAPManager] Initializing store...");
#if UNITY_EDITOR
            StartCoroutine(SimulateEditorInit());
#else
            StartCoroutine(SimulateEditorInit());
#endif
        }

        private IEnumerator SimulateEditorInit()
        {
            yield return new WaitForSeconds(0.3f);
            IsInitialized = true;
            OnStoreInitialized?.Invoke();
        }

        public bool PurchaseProduct(string productId)
        {
            if (!IsInitialized || string.IsNullOrEmpty(productId)) return false;
            var def = _products.Find(p => p.Id == productId);
            if (def == null) return false;
#if UNITY_EDITOR
            StartCoroutine(SimulateEditorPurchase(productId));
#endif
            return true;
        }

        private IEnumerator SimulateEditorPurchase(string productId)
        {
            yield return new WaitForSeconds(0.5f);
            var result = PurchaseResult.Succeeded(productId, $"sim_{Guid.NewGuid():N}");
            OnPurchaseComplete?.Invoke(result);
        }

        public void RestorePurchases() { Debug.Log("[IAPManager] Restoring purchases..."); }
    }
}