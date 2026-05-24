using System;
using System.Threading.Tasks;

namespace UniMart.Services
{
    public class StripePaymentService
    {
        public async Task<(bool IsSuccess, string ResultData)> ProcessPaymentAsync(double amountInRM, string cardNumber, string expMonth, string expYear, string cvc)
        {
            try
            {
                // Simulate the secure network handshake with the payment gateway
                await Task.Delay(2500);

                // Check for the standard PCI-compliance testing card
                if (cardNumber.Replace(" ", "") == "4242424242424242")
                {
                    // Generate a simulated cryptographic Stripe Payment Intent ID for the ledger
                    string simulatedStripeId = "pi_test_" + Guid.NewGuid().ToString("N").Substring(0, 24);

                    return (true, simulatedStripeId);
                }

                // If they type a random card, simulate a bank decline
                return (false, "Your card was declined. Please use the authorized test card.");
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }
    }
}