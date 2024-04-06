using Exo.ColorFormats;
using Exo.Images;
using Exo.Programming.Annotations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ExoImage = Exo.Images.Image;
using ExoRasterizedImage = Exo.Images.RasterizedImage;
using ExoSize = Exo.Images.Size;

namespace Exo.Service;

[Module("Image")]
[TypeId(0x718FB272, 0x914C, 0x43E5, 0x85, 0x5C, 0x2E, 0x91, 0x49, 0xBC, 0x28, 0xB3)]
public sealed class ImageService
{
	public ExoImage Load(string path)
	{
		return null!;
	}

	public ExoImage Background(ArgbColor color) => new ImageSharpBackgroundImage(color.ToBgra32());

	public ExoImage Rectangle(int x, int y, int width, int height) => null!;

	public ExoImage WithConstantMargin(Thickness margin, ExoImage image) => new ImageWithConstantMarginImage(margin, (ImageSharpImage)image);

	private abstract class ImageSharpImage : ExoImage
	{
		private readonly ExoSize _size;

		public override ExoSize Size => _size;

		protected virtual Bgra32 Background => default;

		// TODO: It might be useful to track whether the image has transparency. Except for edge cases where the image would end up opaque "by chance", this property is relatively easy to track.
		//private readonly bool _isTransparent;

		protected ImageSharpImage(ExoSize size) => _size = size;

		// Do the first step of the rendering. Some implementations such as background, may want to do apply specific treatment in that case.
		protected virtual void FirstStepRender(IImageProcessingContext context)
			=> Render(context, new RectangleF(default, context.GetCurrentSize()));

		// Do rendering for any step
		protected internal abstract void Render(IImageProcessingContext context, RectangleF rectangle);

		public sealed override ExoRasterizedImage Rasterize(ExoSize size)
		{
			var baseImage = new Image<Bgra32>(size.Width, size.Height, Background);
			baseImage.Mutate(FirstStepRender);
			return new RasterizedImage(baseImage);
		}

		private sealed class RasterizedImage : ExoRasterizedImage
		{
			private readonly Image<Bgra32> _image;

			public RasterizedImage(Image<Bgra32> image) => _image = image;

			public override ReadOnlyMemory<byte> GetRawBytes()
			{
				if (!_image.Frames[0].DangerousTryGetSinglePixelMemory(out var memory)) throw new InvalidOperationException("Failed to retrieve the raw image data.");

				return default;
			}
		}
	}

	private sealed class ImageSharpBackgroundImage : ImageSharpImage
	{
		private readonly Bgra32 _background;

		protected override Bgra32 Background => _background;

		public override ImageType Type => ImageType.Anchored;

		public ImageSharpBackgroundImage(Bgra32 background) : base(default)
		{
			_background = background;
		}

		// Rendering of the background is already handled
		protected override void FirstStepRender(IImageProcessingContext context) { }

		protected internal override void Render(IImageProcessingContext context, RectangleF rectangle)
		{
			var brush = new SolidBrush(Background);
			if (rectangle.Left == 0 && rectangle.Top == 0)
			{
				var currentSize = context.GetCurrentSize();
				if (rectangle.Width == currentSize.Width && rectangle.Height == currentSize.Height)
				{
					context.Fill(brush);
				}
			}
			context.Fill(brush, new RectangularPolygon(rectangle));
		}
	}

	//internal sealed class ImageSharpScaledFilledRectangleImage : ImageSharpImage
	//{
	//	private readonly RectangleF _rectangle;

	//	protected override void Render(IImageProcessingContext context, RectangleF rectangle)
	//	{
	//	}
	//}

	private sealed class ImageWithConstantMarginImage : ImageSharpImage
	{
		private static ExoSize GetImageSizeWithMargin(Thickness margin, ImageSharpImage image)
		{
			var minSize = image.Type switch
			{
				ImageType.Scaled => new ExoSize(1, 1),
				ImageType.Anchored => image.Size,
				_ => throw new InvalidOperationException(),
			};
			return new(margin.Left + margin.Right + minSize.Width, margin.Top + margin.Bottom + minSize.Height);
		}

		private readonly ImageSharpImage _image;
		private readonly Thickness _margin;

		public override ImageType Type => ImageType.Anchored;

		public ImageWithConstantMarginImage(Thickness margin, ImageSharpImage image)
			: base(GetImageSizeWithMargin(margin, image))
		{
			_image = image;
			_margin = margin;
		}

		protected internal override void Render(IImageProcessingContext context, RectangleF rectangle)
		{
			float imageWidth = rectangle.Width - (_margin.Left + _margin.Right);
			float imageHeight = rectangle.Height - (_margin.Top + _margin.Bottom);

			if (imageWidth <= 0 || imageHeight <= 0) return;

			_image.Render(context, new RectangleF(rectangle.X + _margin.Left, rectangle.Y + _margin.Top, imageWidth, imageHeight));
		}
	}
}
