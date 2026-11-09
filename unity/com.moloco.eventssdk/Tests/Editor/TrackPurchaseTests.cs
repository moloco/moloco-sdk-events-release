using System.Globalization;
using System.Threading;
using NUnit.Framework;
#if MOLOCO_EVENTS_SDK_IAP_5
using System;
using System.Collections.Generic;
using System.Text;
using Purchase = Moloco.MolocoEventsSDK.Purchase;
using System.Reflection;
using UnityEngine.Purchasing;
#endif

namespace Moloco {
    public class TrackPurchaseTests {
        [Test]
        public void FormatPriceUsesPeriodInAnyCulture() {
            var saved = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("1.99", MolocoEventsSDK.FormatPrice(1.99m));
                Assert.AreEqual("1500", MolocoEventsSDK.FormatPrice(1500m));
            } finally {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

#if MOLOCO_EVENTS_SDK_IAP_5
        // Product's constructors are internal to Unity IAP; tests build one through
        // reflection to get a realistic Order.
        private static Product MakeProduct(string id, decimal price, string currency) {
            return MakeProduct(id, id, price, currency);
        }

        private static Product MakeProduct(string id, string storeSpecificId, decimal price, string currency) {
            var definition = new ProductDefinition(id, storeSpecificId, ProductType.Consumable);
            var metadata = new ProductMetadata(price.ToString(CultureInfo.InvariantCulture), "title", "description", currency, price);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 5.4+ adds an availableToPurchase parameter.
            var withAvailability = typeof(Product).GetConstructor(
                flags, null, new[] { typeof(ProductDefinition), typeof(ProductMetadata), typeof(bool) }, null);
            if (withAvailability != null) {
                return (Product)withAvailability.Invoke(new object[] { definition, metadata, true });
            }
            var basic = typeof(Product).GetConstructor(
                flags, null, new[] { typeof(ProductDefinition), typeof(ProductMetadata) }, null);
            return (Product)basic.Invoke(new object[] { definition, metadata });
        }

        private static ICart Cart(string id, decimal price, string currency, int quantity) {
            return new Cart(new CartItem(MakeProduct(id, price, currency), quantity));
        }

        private static IOrderInfo Info(string transactionId) {
            return new StubOrderInfo { TransactionID = transactionId };
        }

        // An Apple order whose signed transaction (JWS) carries this payload.
        private static IOrderInfo AppleInfo(string transactionId, string payloadJson) {
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return new StubOrderInfo {
                TransactionID = transactionId,
                Apple = new StubAppleOrderInfo { jwsRepresentation = "eyJhbGciOiJFUzI1NiJ9." + payload + ".c2ln" },
            };
        }

        private static Purchase SubscriptionOrder(IOrderInfo info) {
            var order = new ConfirmedOrder(Cart("com.foo.pro_monthly", 9.99m, "USD", 1), info);
            return MolocoEventsSDK.ToPurchase(order);
        }

        [Test]
        public void FreeTrialIsReportedAtZero() {
            var purchase = SubscriptionOrder(AppleInfo("t1", "{\"price\":0,\"currency\":\"USD\",\"quantity\":1}"));

            Assert.AreEqual(0m, purchase.Price);
            Assert.AreEqual("USD", purchase.Currency);
        }

        [Test]
        public void DiscountedPriceComesFromTheSignedTransaction() {
            var purchase = SubscriptionOrder(AppleInfo("t1", "{\"price\":4990,\"currency\":\"EUR\",\"quantity\":1}"));

            Assert.AreEqual(4.99m, purchase.Price);
            Assert.AreEqual("EUR", purchase.Currency);
        }

        [Test]
        public void SignedTotalIsDividedIntoUnitPrice() {
            var purchase = SubscriptionOrder(AppleInfo("t1", "{\"price\":14970,\"currency\":\"USD\",\"quantity\":3}"));

            Assert.AreEqual(4.99m, purchase.Price);
            Assert.AreEqual(3, purchase.Quantity);
        }

        [Test]
        public void UrlSafePayloadIsDecoded() {
            // Encodes to base64 containing '+' and '/', so the JWS carries '-' and '_'.
            var json = "{\"price\":4990,\"currency\":\"EUR\",\"quantity\":1,\"note\":\"~~~???\"}";
            var standard = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            Assert.IsTrue(standard.Contains("+") && standard.Contains("/"), "precondition: " + standard);

            Assert.AreEqual(4.99m, SubscriptionOrder(AppleInfo("t1", json)).Price);
        }

        [Test]
        public void RealisticApplePayloadIsRead() {
            var json = "{\"transactionId\":\"2000001257287931\",\"originalTransactionId\":\"2000001257287931\","
                + "\"bundleId\":\"com.olleyo.piggy.match\",\"productId\":\"com.foo.pro_monthly\","
                + "\"purchaseDate\":1761762329000,\"type\":\"Auto-Renewable Subscription\",\"quantity\":1,"
                + "\"storefront\":\"FRA\",\"environment\":\"Production\",\"offerType\":1,"
                + "\"offerDiscountType\":\"PAY_AS_YOU_GO\",\"price\":1990,\"currency\":\"EUR\"}";

            var purchase = SubscriptionOrder(AppleInfo("t1", json));

            Assert.AreEqual(1.99m, purchase.Price);
            Assert.AreEqual("EUR", purchase.Currency);
        }

        [Test]
        public void ZeroQuantityCountsAsOne() {
            var purchase = SubscriptionOrder(AppleInfo("t1", "{\"price\":4990,\"currency\":\"USD\",\"quantity\":0}"));

            Assert.AreEqual(4.99m, purchase.Price);
            Assert.AreEqual(1, purchase.Quantity);
        }

        [Test]
        public void QuantityComesFromTheSignedTransactionEvenWithoutPrice() {
            // Unity IAP builds Apple carts with quantity 1.
            var purchase = SubscriptionOrder(AppleInfo("t1", "{\"currency\":\"USD\",\"quantity\":3}"));

            Assert.AreEqual(9.99m, purchase.Price);
            Assert.AreEqual(3, purchase.Quantity);
        }

        [TestCase("{\"price\":4990,\"quantity\":1}", TestName = "PriceWithoutCurrency")]
        [TestCase("{\"price\":-1,\"currency\":\"USD\"}", TestName = "NegativePrice")]
        [TestCase("{\"price\":null,\"currency\":\"USD\"}", TestName = "NullPrice")]
        [TestCase("{\"offer\":{\"price\":1000},\"currency\":\"USD\"}", TestName = "NestedPriceOnly")]
        [TestCase("[1,2,3]", TestName = "NotAnObject")]
        public void ListedPriceIsUsedWhenTheSignedPriceIsUnusable(string json) {
            Assert.AreEqual(9.99m, SubscriptionOrder(AppleInfo("t1", json)).Price);
        }

        [Test]
        public void ListedPriceIsUsedWhenThePayloadIsNotBase64() {
            var info = new StubOrderInfo {
                TransactionID = "t1", Apple = new StubAppleOrderInfo { jwsRepresentation = "eyJ.!!!.c2ln" },
            };

            Assert.AreEqual(9.99m, SubscriptionOrder(info).Price);
        }

        [Test]
        public void ListedPriceIsUsedWhenTheSignedTransactionHasNoPrice() {
            Assert.AreEqual(9.99m, SubscriptionOrder(AppleInfo("t1", "{\"currency\":\"USD\"}")).Price);
            Assert.AreEqual(9.99m, SubscriptionOrder(new StubOrderInfo {
                TransactionID = "t2", Apple = new StubAppleOrderInfo { jwsRepresentation = "not-a-jws" },
            }).Price);
            Assert.AreEqual(9.99m, SubscriptionOrder(Info("t3")).Price);
        }

        // Unity IAP's OrderInfo is internal.
        private class StubOrderInfo : IOrderInfo {
            public IAppleOrderInfo Apple { get; set; }
            public IGoogleOrderInfo Google => null;
#if MOLOCO_EVENTS_SDK_IAP_5_4_2
            public IPaymentProvidersOrderInfo PaymentProviders => null;
#endif
            public List<IPurchasedProductInfo> PurchasedProductInfo { get; set; } = new List<IPurchasedProductInfo>();
            public string Receipt => "";
            public string TransactionID { get; set; }
        }

        private class StubAppleOrderInfo : IAppleOrderInfo {
            public string AppReceipt => null;
            public string OriginalTransactionID { get; set; }
            public OwnershipType OwnershipType { get; set; }
            public string StoreName { get; set; }
            public Guid? AppAccountToken { get; set; }
            public string jwsRepresentation { get; set; }
        }

        [Test]
        public void OrderWithNoPriceIsLeftToAutomaticCapture() {
            // What Unity IAP builds for a product missing from the fetched catalog.
            var unknown = new ConfirmedOrder(Cart("com.foo.retired", 0m, null, 1), Info("t1"));

            Assert.IsNull(MolocoEventsSDK.ToPurchase(unknown));

            var signed = new ConfirmedOrder(
                Cart("com.foo.retired", 0m, null, 1), AppleInfo("t2", "{\"price\":1990,\"currency\":\"USD\"}"));
            Assert.AreEqual(1.99m, MolocoEventsSDK.ToPurchase(signed).Price);
        }

        [Test]
        public void ConfirmedOrderMapsToPurchase() {
            var order = new ConfirmedOrder(Cart("piggymatch.shop.coinbundle_1.99", 1.99m, "EUR", 2), Info("2000001257287931"));

            var purchase = MolocoEventsSDK.ToPurchase(order);

            Assert.IsNotNull(purchase);
            Assert.AreEqual("piggymatch.shop.coinbundle_1.99", purchase.ProductId);
            Assert.AreEqual(1.99m, purchase.Price);
            Assert.AreEqual("EUR", purchase.Currency);
            Assert.AreEqual("2000001257287931", purchase.TransactionId);
            Assert.AreEqual(2, purchase.Quantity);
        }

        [Test]
        public void OrderReportsAppStoreProductIdNotUnityId() {
            var product = MakeProduct("gems_100", "com.foo.gems100", 0.99m, "USD");
            var order = new ConfirmedOrder(new Cart(new CartItem(product, 1)), Info("t1"));

            var purchase = MolocoEventsSDK.ToPurchase(order);

            Assert.AreEqual("com.foo.gems100", purchase.ProductId);
        }

#if MOLOCO_EVENTS_SDK_IAP_5_4
        [Test]
        public void OrderReadsTheCatalogListingTheCartItemNames() {
            var product = MakeProduct("gems", "com.foo.gems", 0.99m, "USD");
            // A second listing; Unity IAP only builds them from a remote catalog.
            var saleDefinition = new ProductDefinition("gems", "com.foo.gems.sale", ProductType.Consumable, "gems_sale");
            var saleMetadata = new ProductMetadata("0.49", "title", "description", "USD", 0.49m);
            var listing = (CatalogListing)typeof(CatalogListing)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(string), typeof(ProductDefinition), typeof(ProductMetadata), typeof(bool) }, null)
                .Invoke(new object[] { "gems_sale", saleDefinition, saleMetadata, true });
            var listings = (Dictionary<string, CatalogListing>)typeof(Product)
                .GetField("m_CatalogListings", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(product);
            listings["gems_sale"] = listing;
            var order = new ConfirmedOrder(new Cart(new CartItem(product, "gems_sale", 1)), Info("t1"));

            var purchase = MolocoEventsSDK.ToPurchase(order);

            Assert.AreEqual("com.foo.gems.sale", purchase.ProductId);
            Assert.AreEqual(0.49m, purchase.Price);
        }
#endif

        [Test]
        public void PendingOrderMapsToPurchase() {
            var order = new PendingOrder(Cart("gem_pack", 4.99m, "USD", 1), Info("t1"));

            var purchase = MolocoEventsSDK.ToPurchase(order);

            Assert.IsNotNull(purchase);
            Assert.AreEqual("t1", purchase.TransactionId);
        }

        [Test]
        public void OrderWithSeveralItemsIsSkipped() {
            // One transaction ID can carry only one product in the event, and dedupe would
            // drop a second report of it; App Store orders always hold one item.
            var cart = new Cart(new CartItem(MakeProduct("coins_small", 1.99m, "USD"), 1));
            cart.AddItem(new CartItem(MakeProduct("coins_large", 4.99m, "USD"), 1));

            Assert.IsNull(MolocoEventsSDK.ToPurchase(new ConfirmedOrder(cart, Info("t1"))));
        }

        [Test]
        public void FailedDeferredAndNullOrdersAreSkipped() {
            var cart = Cart("gem_pack", 4.99m, "USD", 1);

            Assert.IsNull(MolocoEventsSDK.ToPurchase(new FailedOrder(cart, PurchaseFailureReason.UserCancelled, "")));
            Assert.IsNull(MolocoEventsSDK.ToPurchase(new DeferredOrder(cart, Info("t1"))));
            Assert.IsNull(MolocoEventsSDK.ToPurchase(null));
        }
#endif
    }
}
