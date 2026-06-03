// <copyright file="ImageProcessor.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System;
    
    using UnityEngine;

    /// <summary>
    /// Utility class for processing images and converting between formats.
    /// Provides standardized image processing for the Board SDK.
    /// </summary>
    public static class ImageProcessor
    {
        /// <summary>
        /// Standard cover image width (16:9 aspect ratio).
        /// </summary>
        public const int COVER_WIDTH = 432;

        /// <summary>
        /// Standard cover image height (16:9 aspect ratio).
        /// </summary>
        public const int COVER_HEIGHT = 243;

        /// <summary>
        /// Converts a source texture to a standardized 432x243 PNG byte array for save game cover images.
        /// The image will be scaled and cropped to maintain aspect ratio and exact dimensions.
        /// </summary>
        /// <param name="sourceTexture">The source texture to convert.</param>
        /// <returns>PNG byte array of the processed cover image.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sourceTexture"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Scaling and encoding image process fails.</exception>
        public static byte[] ConvertToStandardizedPNG(Texture2D sourceTexture)
        {
            if (sourceTexture == null)
            {
                throw new ArgumentNullException(nameof(sourceTexture));
            }

            try
            {
                // Scale/crop to exact 432x243 (16:9 aspect ratio)
                var resizedTexture = ScaleTexture(sourceTexture, COVER_WIDTH, COVER_HEIGHT);

                // Convert to PNG bytes
                var pngData = resizedTexture.EncodeToPNG();

                // Clean up temporary texture
                UnityEngine.Object.DestroyImmediate(resizedTexture);

                if (pngData == null || pngData.Length == 0)
                {
                    throw new InvalidOperationException("Failed to encode texture to PNG");
                }

                return pngData;
            }
            catch (Exception ex) when (!(ex is ArgumentNullException))
            {
                throw new InvalidOperationException($"Failed to process cover image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Scales a <see cref="Texture2D"/> to the target dimensions while maintaining aspect ratio and cropping to fit.
        /// Uses high-quality scaling with RenderTexture for best results.
        /// </summary>
        /// <param name="source">Source <see cref="Texture2D"/> to scale.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        /// <returns>A new <see cref="Texture2D"/> with the scaled image.</returns>
        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            // Calculate source aspect ratio
            var sourceAspect = (float)source.width / source.height;
            var targetAspect = (float)targetWidth / targetHeight;

            // Calculate scaling factors to maintain aspect ratio with crop-to-fit
            float scaleX, scaleY;
            if (sourceAspect > targetAspect)
            {
                // Source is wider - scale by height and crop width
                scaleY = 1.0f;
                scaleX = targetAspect / sourceAspect;
            }
            else
            {
                // Source is taller - scale by width and crop height
                scaleX = 1.0f;
                scaleY = sourceAspect / targetAspect;
            }

            // Create RenderTexture for high-quality scaling
            var renderTexture =
                RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = renderTexture;

            // Create material for blitting with proper scaling
            var blitMaterial = new Material(Shader.Find("Hidden/Internal-GUITexture"));

            // Calculate UV coordinates for cropping
            var offsetX = (1.0f - scaleX) * 0.5f;
            var offsetY = (1.0f - scaleY) * 0.5f;

            // Set up the blit with cropping
            var scale = new Vector2(scaleX, scaleY);
            var offset = new Vector2(offsetX, offsetY);

            // Clear the render texture
            GL.Clear(true, true, Color.clear);

            // Blit with scaling and cropping
            Graphics.Blit(source, renderTexture, scale, offset);

            // Create new Texture2D and read pixels from RenderTexture
            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.DestroyImmediate(blitMaterial);

            return result;
        }

        /// <summary>
        /// Validates that a <see cref="Texture2D"/> is suitable for use as a cover image.
        /// </summary>
        /// <param name="texture"><see cref="Texture2D"/> to validate.</param>
        /// <returns><c>true</c> if the texture is valid for use as a cover image; otherwise, <c>false</c>.</returns>
        public static bool IsValidCoverImage(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            // Check minimum dimensions (we can scale up, but very small images won't look good)
            if (texture.width < 64 || texture.height < 64)
            {
                return false;
            }

            // Check that texture is readable
            try
            {
                texture.GetPixel(0, 0);
                return true;
            }
            catch (UnityException)
            {
                // Texture is not readable
                return false;
            }
        }

        /// <summary>
        /// Converts a byte array containing PNG image data to a Texture2D.
        /// </summary>
        /// <param name="pngBytes">The PNG image data as bytes.</param>
        /// <returns>A <see cref="Texture2D"/> containing the loaded image if successful; otherwise <c>null</c>.</returns>
        public static Texture2D LoadTextureFromPNG(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return null;
            }

            try
            {
                // Create a texture (size doesn't matter, LoadImage will replace it)
                var texture = new Texture2D(2, 2);

                // Load the PNG data into the texture
                if (ImageConversion.LoadImage(texture, pngBytes))
                {
                    return texture;
                }
                
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a test cover image for development/testing purposes.
        /// </summary>
        /// <param name="backgroundColor">Background color for the test image.</param>
        /// <param name="text">Optional text to display on the image.</param>
        /// <returns>A new Texture2D with test content.</returns>
        public static Texture2D CreateTestCoverImage(Color backgroundColor = default, string text = null)
        {
            if (backgroundColor == default)
            {
                backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1.0f); // Nice blue
            }

            var texture = new Texture2D(COVER_WIDTH, COVER_HEIGHT, TextureFormat.RGBA32, false);

            // Fill with background color
            var pixels = new Color[COVER_WIDTH * COVER_HEIGHT];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }

            // Add some visual pattern
            for (var y = 0; y < COVER_HEIGHT; y++)
            {
                for (var x = 0; x < COVER_WIDTH; x++)
                {
                    var index = y * COVER_WIDTH + x;

                    // Add gradient effect
                    var gradient = (float)x / COVER_WIDTH;
                    pixels[index] = Color.Lerp(backgroundColor, Color.white, gradient * 0.2f);

                    // Add border
                    if (x == 0 || x == COVER_WIDTH - 1 || y == 0 || y == COVER_HEIGHT - 1)
                    {
                        pixels[index] = Color.white;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}
