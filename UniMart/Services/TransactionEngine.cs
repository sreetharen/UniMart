using Plugin.CloudFirestore;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UniMart.Models;
using System.Linq;

namespace UniMart.Services
{
    public class TransactionEngine
    {
        private readonly IFirestore _db = CrossCloudFirestore.Current.Instance;

        /// <summary>
        /// Handles Stripe Top-ups.
        /// </summary>
        public async Task<bool> TopUpBalanceAsync(string userId, double amount, string stripeId)
        {
            try
            {
                var userRef = _db.Collection("users").Document(userId);
                string receiptId = Guid.NewGuid().ToString();
                DateTime now = DateTime.UtcNow;

                await _db.RunTransactionAsync(transaction =>
                {
                    transaction.Update(userRef, "wallet.availableBalance", FieldValue.Increment(amount));

                    var receipt = new TransactionRecord
                    {
                        Id = receiptId,
                        UserId = userId,
                        Amount = amount,
                        Type = "TopUp",
                        // 🔥 UX FIX: Removed the ugly 'pi_test' stripe ID
                        Description = "Wallet Top-Up via Stripe",
                        Date = now
                    };
                    transaction.Set(_db.Collection("transactions").Document(receiptId), receipt);
                });
                return true;
            }
            catch { return false; }
        }

        public async Task<string> AcceptOfferAndHoldEscrow(string offerId, string listingId, string buyerId, double offerAmount, string sellerId)
        {
            var buyerRef = _db.Collection("users").Document(buyerId);
            var offerRef = _db.Collection("offers").Document(offerId);
            var listingRef = _db.Collection("listings").Document(listingId);

            try
            {
                string secretHandoverToken = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                string receiptId = Guid.NewGuid().ToString();
                DateTime now = DateTime.UtcNow;

                await _db.RunTransactionAsync(transaction =>
                {
                    var listingSnapshot = transaction.Get(listingRef);
                    var listing = listingSnapshot.ToObject<Listing>();

                    if (listing == null || listing.Status != "Available") throw new Exception("Item is no longer available.");

                    var buyerSnapshot = transaction.Get(buyerRef);
                    var buyer = buyerSnapshot.ToObject<User>();

                    if (buyer == null || buyer.Wallet.AvailableBalance < offerAmount) throw new Exception("Insufficient RM balance.");

                    transaction.Update(buyerRef, "wallet.availableBalance", FieldValue.Increment(-offerAmount));
                    transaction.Update(buyerRef, "wallet.pendingEscrow", FieldValue.Increment(offerAmount));
                    transaction.Update(listingRef, "status", "Reserved");
                    transaction.Update(listingRef, "soldToBuyerId", buyerId);

                    var newOffer = new Offer
                    {
                        Id = offerId,
                        ListingId = listingId,
                        BuyerId = buyerId,
                        SellerId = sellerId,
                        Participants = new List<string> { buyerId, sellerId },
                        ListingTitlePreview = listing.Title,
                        ListingImagePreview = listing.CoverImage,
                        LastMessageText = "Purchase confirmed! Coordination required.",
                        LastMessageTime = now,
                        OfferPrice = offerAmount,
                        Status = "Accepted",
                        HandoverToken = secretHandoverToken,
                        BuyerReceiptId = receiptId,
                        EscrowState = new EscrowState { HeldAmount = offerAmount, Status = "Held" },
                        Timestamp = now
                    };
                    transaction.Set(offerRef, newOffer);

                    var buyerReceipt = new TransactionRecord
                    {
                        Id = receiptId,
                        UserId = buyerId,
                        Amount = offerAmount,
                        Type = "Purchase",
                        Description = $"Reserved: {listing.Title}",
                        Date = now
                    };
                    transaction.Set(_db.Collection("transactions").Document(receiptId), buyerReceipt);
                });

                return secretHandoverToken;
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); return null; }
        }

        public async Task<bool> FinalizeHandover(string offerId, string scannedToken, string currentUserId)
        {
            var offerRef = _db.Collection("offers").Document(offerId);

            try
            {
                DateTime now = DateTime.UtcNow;

                await _db.RunTransactionAsync(transaction =>
                {
                    var offerSnapshot = transaction.Get(offerRef);
                    var offer = offerSnapshot.ToObject<Offer>();

                    if (offer == null || offer.HandoverToken != scannedToken || (offer.Status != "Accepted" && offer.Status != "Inquiry"))
                        throw new Exception("Invalid Handover QR Code.");

                    if (offer.SellerId != currentUserId) throw new Exception("Unauthorized.");

                    var listingRef = _db.Collection("listings").Document(offer.ListingId);
                    var buyerRef = _db.Collection("users").Document(offer.BuyerId);
                    var sellerRef = _db.Collection("users").Document(offer.SellerId);

                    transaction.Update(buyerRef, "wallet.pendingEscrow", FieldValue.Increment(-offer.EscrowState.HeldAmount));
                    transaction.Update(sellerRef, "wallet.availableBalance", FieldValue.Increment(offer.EscrowState.HeldAmount));
                    transaction.Update(offerRef, "status", "Completed");
                    transaction.Update(offerRef, "escrowState.status", "Released");
                    transaction.Update(offerRef, "lastMessageText", "✅ Handover Completed successfully!");
                    transaction.Update(offerRef, "lastMessageTime", now);
                    transaction.Update(listingRef, "status", "Sold");

                    if (!string.IsNullOrEmpty(offer.BuyerReceiptId))
                        transaction.Update(_db.Collection("transactions").Document(offer.BuyerReceiptId), "description", "Purchase Completed");

                    string sellerReceiptId = Guid.NewGuid().ToString();
                    transaction.Set(_db.Collection("transactions").Document(sellerReceiptId), new TransactionRecord
                    {
                        Id = sellerReceiptId,
                        UserId = offer.SellerId,
                        Amount = offer.EscrowState.HeldAmount,
                        Type = "Sale",
                        Description = "Item Sold (Payout)",
                        Date = now
                    });
                });
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> CancelAndRefundEscrow(string offerId, string currentUserId)
        {
            var offerRef = _db.Collection("offers").Document(offerId);

            try
            {
                DateTime now = DateTime.UtcNow;

                await _db.RunTransactionAsync(transaction =>
                {
                    var offerSnapshot = transaction.Get(offerRef);
                    var offer = offerSnapshot.ToObject<Offer>();

                    if (offer == null || offer.Status != "Accepted") throw new Exception("Invalid order state.");

                    var buyerRef = _db.Collection("users").Document(offer.BuyerId);
                    var listingRef = _db.Collection("listings").Document(offer.ListingId);
                    double refundAmount = offer.EscrowState.HeldAmount;

                    transaction.Update(buyerRef, "wallet.pendingEscrow", FieldValue.Increment(-refundAmount));
                    transaction.Update(buyerRef, "wallet.availableBalance", FieldValue.Increment(refundAmount));
                    transaction.Update(offerRef, "status", "Cancelled");
                    transaction.Update(offerRef, "escrowState.status", "Refunded");
                    transaction.Update(offerRef, "lastMessageText", "❌ Transaction Cancelled & Refunded.");
                    transaction.Update(offerRef, "lastMessageTime", now);
                    transaction.Update(listingRef, "status", "Available");
                    transaction.Update(listingRef, "soldToBuyerId", FieldValue.Delete);

                    // 🔥 LOGIC FIX: Generating the missing Refund Receipt!
                    string refundReceiptId = Guid.NewGuid().ToString();
                    var refundReceipt = new TransactionRecord
                    {
                        Id = refundReceiptId,
                        UserId = offer.BuyerId,
                        Amount = refundAmount,
                        Type = "Refund",
                        Description = $"Refund: {offer.ListingTitlePreview}",
                        Date = now
                    };
                    transaction.Set(_db.Collection("transactions").Document(refundReceiptId), refundReceipt);
                });
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> DisputeEscrow(string offerId, string currentUserId)
        {
            var offerRef = _db.Collection("offers").Document(offerId);
            try
            {
                await _db.RunTransactionAsync(transaction =>
                {
                    var offerSnapshot = transaction.Get(offerRef);
                    var offer = offerSnapshot.ToObject<Offer>();
                    if (offer == null || offer.Status != "Accepted") throw new Exception("Unauthorized dispute.");

                    transaction.Update(offerRef, "status", "Disputed");
                    transaction.Update(offerRef, "escrowState.status", "Frozen");
                    transaction.Update(offerRef, "lastMessageText", "⚠️ Payment Frozen (Dispute Opened)");
                    transaction.Update(offerRef, "lastMessageTime", DateTime.UtcNow);
                    transaction.Update(_db.Collection("listings").Document(offer.ListingId), "status", "Disputed");
                });
                return true;
            }
            catch { return false; }
        }
    }
}