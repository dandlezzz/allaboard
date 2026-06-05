> Source: https://docs.dev.board.fun/unity/api/ImageProcessor.html — fetched 2026-06-04T18:38 (UTC-7)

# ImageProcessor Class (Board.Save)

Utility class for processing images and converting between formats. Provides standardized image processing for the Board SDK.

```csharp
public static class ImageProcessor
```

Inheritance: System.Object 🡒 ImageProcessor

| Fields | Description |
| --- | --- |
| COVER_HEIGHT | Standard cover image height (16:9 aspect ratio). |
| COVER_WIDTH | Standard cover image width (16:9 aspect ratio). |

| Methods | Description |
| --- | --- |
| ConvertToStandardizedPNG(Texture2D) | Converts a source texture to a standardized 432x243 PNG byte array for save game cover images. The image will be scaled and cropped to maintain aspect ratio and exact dimensions. |
| CreateTestCoverImage(Color, string) | Creates a test cover image for development/testing purposes. |
| IsValidCoverImage(Texture2D) | Validates that a UnityEngine.Texture2D is suitable for use as a cover image. |
| LoadTextureFromPNG(byte[]) | Converts a byte array containing PNG image data to a Texture2D. |
