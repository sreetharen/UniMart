using Plugin.CloudFirestore.Attributes;
using System;
using System.Collections.Generic;

namespace UniMart.Models
{
    public class Offer
    {
        [Id]
        public string Id { get; set; }

        [MapTo("listingId")]
        public string ListingId { get; set; }

        [MapTo("buyerId")]
        public string BuyerId { get; set; }

        [MapTo("sellerId")]
        public string SellerId { get; set; }

        [MapTo("buyerName")]
        public string BuyerName { get; set; }

        [MapTo("sellerName")]
        public string SellerName { get; set; }

        [MapTo("unreadCounts")]
        public Dictionary<string, int> UnreadCounts { get; set; } = new Dictionary<string, int>();

        [MapTo("participants")]
        public List<string> Participants { get; set; } = new();

        [MapTo("lastMessageText")]
        public string LastMessageText { get; set; }

        [MapTo("lastMessageTime")]
        public DateTime LastMessageTime { get; set; }

        [MapTo("listingTitlePreview")]
        public string ListingTitlePreview { get; set; }

        [MapTo("listingImagePreview")]
        public string ListingImagePreview { get; set; }

        [MapTo("offerPrice")]
        public double OfferPrice { get; set; }

        [MapTo("status")]
        public string Status { get; set; }

        [MapTo("handoverToken")]
        public string HandoverToken { get; set; }

        [MapTo("buyerReceiptId")]
        public string BuyerReceiptId { get; set; }

        [MapTo("escrowState")]
        public EscrowState EscrowState { get; set; } = new EscrowState();

        [MapTo("timestamp")]
        public DateTime Timestamp { get; set; }

        // EXPLICIT CONSTRUCTOR FOR FIREBASE
        public Offer() { }

        // 🔥 THE LINKER SHIELD
        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(Offer))]
        public static void Preserve() { new Offer(); }
    }

    public class EscrowState
    {
        [MapTo("status")]
        public string Status { get; set; } = "None";

        [MapTo("heldAmount")]
        public double HeldAmount { get; set; }

        public EscrowState() { }

        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(EscrowState))]
        public static void Preserve() { new EscrowState(); }
    }
}