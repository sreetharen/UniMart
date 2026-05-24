namespace UniMart.Config
{
    public static class AppSettings
    {
        // ==========================================
        // STRIPE (Payment Gateway)
        // ==========================================
        // FYP Defense Note: In a live enterprise app, this secret key lives on a secure web server.
        // For this frontend-only FYP prototype, we safely simulate the server environment here.
        public const string StripeSecretKey = "sk_test_51TP1uLFbymg5nd5UQJncw7BZYirl1R7fYhWDo37Hsk318rtBZOUTaYdAFoLmTnObcb5fo2P0z2w4oCurfOmt2lYr00gzPzYpOV";

        // ==========================================
        // CLOUDINARY (Image CDN Pipeline)
        // ==========================================
        public const string CloudinaryCloudName = "dfwqvbtu1";
        public const string CloudinaryApiKey = "444394894133528";
        public const string CloudinaryApiSecret = "IHpwEwtbO4zdtHGbfaoU55Ux9Jo";
    }
}