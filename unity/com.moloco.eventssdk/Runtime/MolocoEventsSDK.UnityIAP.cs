#if MOLOCO_EVENTS_SDK_IAP_5
using System;
using System.Text;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Scripting;

namespace Moloco {
    // Compiled only when Unity IAP 5 is installed (versionDefines in Moloco.EventsSDK.asmdef).
    public static partial class MolocoEventsSDK {
        /// <summary>
        /// Reports a Unity IAP 5 order as an <c>in_app_purchase</c> event. Call it from
        /// <c>OnPurchaseConfirmed</c>. Unity IAP redelivers unconfirmed orders to
        /// <c>OnPurchasePending</c> on every launch, so reporting there can send a
        /// purchase again in a later app run. Failed and deferred orders, and orders with
        /// more than one item, are ignored, as are orders with no price available.
        /// Sends the price charged, read from Apple's signed transaction, so a free
        /// trial is sent as 0. Without one (StoreKit 1 path) it sends the listed price.
        /// </summary>
        public static void TrackPurchase(Order order) {
            var purchase = ToPurchase(order);
            if (purchase == null) return;
            TrackPurchase(purchase.ProductId, purchase.Price, purchase.Currency, purchase.TransactionId, purchase.Quantity);
        }

        internal static Purchase ToPurchase(Order order) {
            if (!(order is PendingOrder || order is ConfirmedOrder)) return null;
            var items = order.CartOrdered?.Items();
            if (items == null || items.Count == 0) return null;
            // App Store orders hold one item. The event carries one product per
            // transaction ID, and dedupe would drop a second report of the same ID.
            if (items.Count > 1) {
                Debug.LogWarning("[MolocoEventsSDK] TrackPurchase(Order) skipped an order with "
                    + items.Count + " items; only single-item orders are supported.");
                return null;
            }
            var item = items[0];
            var product = item.Product;
            if (product == null) return null;
            var definition = product.definition;
            var metadata = product.metadata;
#if MOLOCO_EVENTS_SDK_IAP_5_4
            // The listing the cart item names, not the product's default one.
            if (item.CatalogListingId != null
                && product.catalogListings.TryGetValue(item.CatalogListingId, out var listing)) {
                definition = listing.definition;
                metadata = listing.metadata;
            }
#endif
            if (definition == null || metadata == null) return null;
            var purchase = new Purchase {
                // The App Store product id, as automatic capture reports it.
                ProductId = string.IsNullOrEmpty(definition.storeSpecificId) ? definition.id : definition.storeSpecificId,
                Price = metadata.localizedPrice,
                Currency = metadata.isoCurrencyCode,
                TransactionId = order.Info?.TransactionID,
                Quantity = item.Quantity,
            };
            // The listed price misses free trials and offers; the signed transaction has
            // what was charged. It is absent on the StoreKit 1 path (iOS 14 and below, or
            // when Unity IAP is set to StoreKit 1), which keeps the listed price.
            var signed = ReadSignedTransaction(order.Info?.Apple?.jwsRepresentation);
            if (signed != null) {
                // Unity IAP builds Apple carts with quantity 1.
                if (signed.quantity > 0) purchase.Quantity = signed.quantity;
                if (long.TryParse(signed.price, out var milliunits) && milliunits >= 0
                    && !string.IsNullOrEmpty(signed.currency)) {
                    // Milliunits, and the total for the quantity.
                    purchase.Price = milliunits / 1000m / Math.Max(purchase.Quantity, 1);
                    purchase.Currency = signed.currency;
                }
            }
            // A product missing from the fetched catalog has no listed price. Without a
            // signed price, leave it to automatic capture instead of sending 0.
            if (string.IsNullOrEmpty(purchase.Currency)) {
                Debug.LogWarning("[MolocoEventsSDK] TrackPurchase(Order) skipped " + purchase.ProductId
                    + ": no price available; automatic capture reports it.");
                return null;
            }
            return purchase;
        }

        // Reads, without verifying, the payload of a StoreKit 2 signed transaction.
        static SignedTransaction ReadSignedTransaction(string jws) {
            var parts = jws?.Split('.');
            if (parts == null || parts.Length != 3) return null;
            try {
                var base64 = parts[1].Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                return JsonUtility.FromJson<SignedTransaction>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            } catch (FormatException) {
                return null;
            } catch (ArgumentException) {
                return null;
            }
        }

        // `price` as a string so a missing or null top-level price stays empty instead of
        // reading as 0 (JsonUtility also accepts a JSON number into a string field).
        [Serializable, Preserve]
        class SignedTransaction {
            public string price;
            public string currency;
            public int quantity;
        }

        internal sealed class Purchase {
            public string ProductId;
            public decimal Price;
            public string Currency;
            public string TransactionId;
            public int Quantity;
        }
    }
}
#endif
