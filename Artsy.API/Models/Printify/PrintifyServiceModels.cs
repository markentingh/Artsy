using System.Text.Json.Serialization;

namespace Artsy.API.Models.Printify
{
    public class PrintifyShopResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("sales_channel")]
        public string SalesChannel { get; set; } = "";
    }

    public class PrintifyUploadResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = "";

        [JsonPropertyName("preview_url")]
        public string PreviewUrl { get; set; } = "";

        [JsonPropertyName("upload_time")]
        public string UploadTime { get; set; } = "";
    }

    public class PrintifyProductRequest
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("safety_information")]
        public string SafetyInformation { get; set; } = "";

        [JsonPropertyName("blueprint_id")]
        public int BlueprintId { get; set; }

        [JsonPropertyName("print_provider_id")]
        public int PrintProviderId { get; set; }

        [JsonPropertyName("variants")]
        public List<PrintifyVariantRequest> Variants { get; set; } = new();

        [JsonPropertyName("print_areas")]
        public List<PrintifyPrintAreaRequest> PrintAreas { get; set; } = new();
    }

    public class PrintifyProductImageRequest
    {
        [JsonPropertyName("src")]
        public string Src { get; set; } = "";

        [JsonPropertyName("variant_ids")]
        public List<int> VariantIds { get; set; } = new();

        [JsonPropertyName("position")]
        public string Position { get; set; } = "front";

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }
    }

    public class PrintifyVariantRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("is_enabled")]
        public bool IsEnabled { get; set; }
    }

    public class PrintifyPrintAreaRequest
    {
        [JsonPropertyName("variant_ids")]
        public List<int> VariantIds { get; set; } = new();

        [JsonPropertyName("placeholders")]
        public List<PrintifyPlaceholderRequest> Placeholders { get; set; } = new();
    }

    public class PrintifyPlaceholderRequest
    {
        [JsonPropertyName("position")]
        public string Position { get; set; } = "";

        [JsonPropertyName("images")]
        public List<PrintifyPlaceholderImageRequest> Images { get; set; } = new();
    }

    public class PrintifyPlaceholderImageRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("x")]
        public double X { get; set; } = 0.5;

        [JsonPropertyName("y")]
        public double Y { get; set; } = 0.5;

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 1;

        [JsonPropertyName("angle")]
        public double Angle { get; set; } = 0;

        [JsonPropertyName("pattern")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PrintifyPatternRequest? Pattern { get; set; }
    }

    public class PrintifyPatternRequest
    {
        [JsonPropertyName("spacing_x")]
        public int SpacingX { get; set; }

        [JsonPropertyName("spacing_y")]
        public int SpacingY { get; set; }

        [JsonPropertyName("scale")]
        public int Scale { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }

    public class PrintifyProductResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("safety_information")]
        public string SafetyInformation { get; set; } = "";

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }

        [JsonPropertyName("is_locked")]
        public bool IsLocked { get; set; }

        [JsonPropertyName("blueprint_id")]
        public int BlueprintId { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("shop_id")]
        public int ShopId { get; set; }

        [JsonPropertyName("print_provider_id")]
        public int PrintProviderId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = "";

        [JsonPropertyName("variants")]
        public List<PrintifyProductVariantResponse> Variants { get; set; } = new();

        [JsonPropertyName("images")]
        public List<PrintifyProductImageResponse> Images { get; set; } = new();
    }

    public class PrintifyProductVariantResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = "";

        [JsonPropertyName("cost")]
        public int Cost { get; set; }

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("grams")]
        public int Grams { get; set; }

        [JsonPropertyName("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("is_available")]
        public bool IsAvailable { get; set; }
    }

    public class PrintifyProductImageResponse
    {
        [JsonPropertyName("src")]
        public string Src { get; set; } = "";

        [JsonPropertyName("variant_ids")]
        public List<int> VariantIds { get; set; } = new();

        [JsonPropertyName("position")]
        public string Position { get; set; } = "";

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }
    }

    public class PrintifyPublishRequest
    {
        [JsonPropertyName("title")]
        public bool Title { get; set; }

        [JsonPropertyName("description")]
        public bool Description { get; set; }

        [JsonPropertyName("images")]
        public bool Images { get; set; }

        [JsonPropertyName("variants")]
        public bool Variants { get; set; }

        [JsonPropertyName("tags")]
        public bool Tags { get; set; }

        [JsonPropertyName("keyFeatures")]
        public bool KeyFeatures { get; set; }

        [JsonPropertyName("shipping_template")]
        public bool ShippingTemplate { get; set; }
    }

    public class PrintifyError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("errors")]
        public PrintifyErrorDetails? Errors { get; set; }
    }

    public class PrintifyErrorDetails
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "";
    }

    public class PrintifyProductResult
    {
        public PrintifyProductResponse? Product { get; set; }
        public string? Error { get; set; }
        public bool Success => Product != null && string.IsNullOrEmpty(Error);
    }
}
