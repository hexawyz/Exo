using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace Exo.Service;

internal sealed class CircleCroppingProcessor : IImageProcessor
{
	private readonly Color _color;
	private readonly byte _tileShift;

	public CircleCroppingProcessor(Color color, byte tileShift)
	{
		_color = color;
		_tileShift = tileShift;
	}

	public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(SixLabors.ImageSharp.Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
		where TPixel : unmanaged, IPixel<TPixel>
		=> new CircleCroppingProcessor<TPixel>(configuration, source, sourceRectangle, _color.ToPixel<TPixel>(), _tileShift);
}

internal sealed class CircleCroppingProcessor<TPixel> : ImageProcessor<TPixel>
	where TPixel : unmanaged, IPixel<TPixel>
{
	private readonly TPixel _color;
	private readonly byte _tileShift;

	public CircleCroppingProcessor(SixLabors.ImageSharp.Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle, TPixel color, byte tileShift)
		: base(configuration, source, sourceRectangle)
	{
		if (tileShift > 8) throw new ArgumentOutOfRangeException(nameof(tileShift));
		_color = color;
		_tileShift = tileShift;
	}

	protected override void OnFrameApply(ImageFrame<TPixel> source)
	{
		// Most operations are optimized for circle and not for ellipse because we don't really care that much about ellipses (at least yet)
		if (source.Width == source.Height)
		{
			if (_tileShift == 0)
			{
				if (SourceRectangle == new Rectangle(0, 0, source.Width, source.Height))
				{
					var operation = new UncroppedCircleRowIntervalOperation(source.PixelBuffer, _color);
					ParallelRowIterator.IterateRowIntervals(Configuration, SourceRectangle, in operation);
				}
				else
				{
					var operation = new CircleRowIntervalOperation(SourceRectangle, source.PixelBuffer, _color);
					ParallelRowIterator.IterateRowIntervals(Configuration, SourceRectangle, in operation);
				}
			}
			else
			{
				if (SourceRectangle == new Rectangle(0, 0, source.Width, source.Height))
				{
					var operation = new UncroppedTiledCircleRowIntervalOperation(source.PixelBuffer, _color, _tileShift);
					ParallelRowIterator.IterateRowIntervals(Configuration, SourceRectangle, in operation);
				}
				else
				{
					var operation = new TiledCircleRowIntervalOperation(SourceRectangle, source.PixelBuffer, _color, _tileShift);
					ParallelRowIterator.IterateRowIntervals(Configuration, SourceRectangle, in operation);
				}
			}
		}
		else
		{
			var operation = new TiledEllipseRowIntervalOperation(SourceRectangle, source.PixelBuffer, _color, _tileShift);

			ParallelRowIterator.IterateRowIntervals(Configuration, SourceRectangle, in operation);
		}
	}

	// This is the reference algorithm.
	// All other versions are simplified from this one.
	private readonly struct TiledEllipseRowIntervalOperation : IRowIntervalOperation
	{
		private readonly Rectangle _bounds;
		private readonly Buffer2D<TPixel> _pixels;
		private readonly TPixel _color;
		private readonly double _ratio;
		private readonly int _maxX;
		private readonly int _midPointY;
		private readonly int _tileSize;
		private readonly int _tileMask;

		public TiledEllipseRowIntervalOperation(Rectangle bounds, Buffer2D<TPixel> pixels, TPixel color, byte tileShift)
		{
			_bounds = bounds;
			_pixels = pixels;
			_color = color;
			_maxX = _pixels.Width - 1;
			// The vertical radius of should be exactly H/2
			_midPointY = _pixels.Height >>> 1;
			_tileSize = (1 << tileShift);
			_tileMask = _tileSize - 1;
			_ratio = (double)_pixels.Width / _pixels.Height;
		}

		public void Invoke(in RowInterval rows)
		{
			for (int i = rows.Min; i < rows.Max;)
			{
				// We can avoid doing the circle computation for every row in the tile.
				// To that effect, it is easy to compute the upper bound of the tile.
				int tileMaxY = (i + _tileSize) & ~_tileMask;

				// The most important thing to do is to make y closer to the center.
				// The reason behind that is that we want to be optimistic towards the fact that the point would be "inside".
				// For that reason, every "pixel" or "tile" should use the coordinate that points towards the center.
				// Remember that pixels are considered to be rectangles of width 1x1. Tiles are larger. If more than 0% of the rectangle overlaps the circle, then it is "inside".
				// NB: We may want to handle the boundary condition at the edges. Basically, if the corner is exactly on the circle, we want to consider the rectangle "outside".
				// Regarding the computation below, an easy way to compute the lower coordinate of the tile is to add 2^(N-1) before masking out the bits of the tile.
				// However, because in the upper half, we already want to adjust the pixel coordinate by 1 below, the "-1" is nullified.
				int y;
				if (i < _midPointY) y = Math.Min(tileMaxY, _midPointY);
				// In the lower half case, the value needs to be reflected around the middle, in order to obtain the equivalent y in the upper quadrant.
				else if (i > _midPointY) y = _pixels.Height - Math.Max((i - _tileMask) & ~_tileMask, _midPointY);
				else y = i;

				// The below formula relies on basic trigonometry (sin² + cos² = 1) but adapted for the specifics of the current computation.
				// This does use a few FP operations (although the minimum possible), and it might be possible to switch to a full integer algorithm at some point.
				// But for now, this will do.
				int x0 = (_pixels.Width - (int)(Math.Floor(Math.Sqrt((y * (_pixels.Height - y)) << 2) * _ratio))) >> 1;
				int x1 = (_maxX - x0 + _tileMask) & ~_tileMask;
				int w0 = Math.Max(0, (x0 & ~_tileMask) - _bounds.Left);
				int w1 = Math.Max(0, _bounds.Right - x1);
				x0 = _bounds.Left;

				// We shall now mask out the pixels of each row in bulk.
				for (; i < tileMaxY; i++)
				{
					var row = _pixels.DangerousGetRowSpan(i);
					row.Slice(x0, w0).Fill(_color);
					row.Slice(x1, w1).Fill(_color);
				}
			}
		}
	}

	private readonly struct TiledCircleRowIntervalOperation : IRowIntervalOperation
	{
		private readonly Rectangle _bounds;
		private readonly Buffer2D<TPixel> _pixels;
		private readonly TPixel _color;
		private readonly int _maxX;
		private readonly int _midPointY;
		private readonly int _tileSize;
		private readonly int _tileMask;

		public TiledCircleRowIntervalOperation(Rectangle bounds, Buffer2D<TPixel> pixels, TPixel color, byte tileShift)
		{
			_bounds = bounds;
			_pixels = pixels;
			_color = color;
			_maxX = _pixels.Width - 1;
			_midPointY = _pixels.Height >>> 1;
			_tileSize = 1 << tileShift;
			_tileMask = _tileSize - 1;
		}

		public void Invoke(in RowInterval rows)
		{
			for (int i = rows.Min; i < rows.Max;)
			{
				int tileMaxY = (i + _tileSize) & ~_tileMask;

				int y;
				if (i < _midPointY) y = Math.Min(tileMaxY, _midPointY);
				else if (i > _midPointY) y = _pixels.Height - Math.Max((i - _tileMask) & ~_tileMask, _midPointY);
				else y = i;

				int x0 = (_pixels.Width - (int)Math.Floor(Math.Sqrt((y * (_pixels.Height - y)) << 2))) >> 1;
				int x1 = (_maxX - x0 + _tileMask) & ~_tileMask;
				int w0 = Math.Max(0, (x0 & ~_tileMask) - _bounds.Left);
				int w1 = Math.Max(0, _bounds.Right - x1);
				x0 = _bounds.Left;

				for (; i < tileMaxY; i++)
				{
					var row = _pixels.DangerousGetRowSpan(i);
					row.Slice(x0, w0).Fill(_color);
					row.Slice(x1, w1).Fill(_color);
				}
			}
		}
	}

	private readonly struct CircleRowIntervalOperation : IRowIntervalOperation
	{
		private readonly Rectangle _bounds;
		private readonly Buffer2D<TPixel> _pixels;
		private readonly TPixel _color;
		private readonly int _maxX;
		private readonly int _midPointY;

		public CircleRowIntervalOperation(Rectangle bounds, Buffer2D<TPixel> pixels, TPixel color)
		{
			_bounds = bounds;
			_pixels = pixels;
			_color = color;
			_maxX = _pixels.Width - 1;
			_midPointY = _pixels.Height >>> 1;
		}

		public void Invoke(in RowInterval rows)
		{
			for (int i = rows.Min; i < rows.Max; i++)
			{
				int y;
				if (i < _midPointY) y = i + 1;
				else if (i > _midPointY) y = _pixels.Height - i;
				else y = i;

				int x0 = (_pixels.Width - (int)Math.Floor(Math.Sqrt((y * (_pixels.Height - y)) << 2))) >> 1;
				int x1 = _maxX - x0;
				int w0 = Math.Max(0, x0 - _bounds.Left);
				int w1 = Math.Max(0, _bounds.Right - x1);
				x0 = _bounds.Left;

				var row = _pixels.DangerousGetRowSpan(i);
				row.Slice(x0, w0).Fill(_color);
				row.Slice(x1, w1).Fill(_color);
			}
		}
	}

	private readonly struct UncroppedTiledCircleRowIntervalOperation : IRowIntervalOperation
	{
		private readonly Buffer2D<TPixel> _pixels;
		private readonly TPixel _color;
		private readonly int _maxX;
		private readonly int _midPointY;
		private readonly int _tileSize;
		private readonly int _tileMask;

		public UncroppedTiledCircleRowIntervalOperation(Buffer2D<TPixel> pixels, TPixel color, byte tileShift)
		{
			_pixels = pixels;
			_color = color;
			_maxX = _pixels.Width - 1;
			_midPointY = _pixels.Height >>> 1;
			_tileSize = 1 << tileShift;
			_tileMask = _tileSize - 1;
		}

		public void Invoke(in RowInterval rows)
		{
			for (int i = rows.Min; i < rows.Max;)
			{
				int tileMaxY = (i + _tileSize) & ~_tileMask;

				int y;
				if (i < _midPointY) y = Math.Min(tileMaxY, _midPointY);
				else if (i > _midPointY) y = _pixels.Height - Math.Max((i - _tileMask) & ~_tileMask, _midPointY);
				else y = i;

				int x0 = (_pixels.Width - (int)Math.Floor(Math.Sqrt((y * (_pixels.Height - y)) << 2))) >> 1;
				int x1 = (_maxX - x0 + _tileMask) & ~_tileMask;
				x0 &= ~_tileMask;
				int w1 = _pixels.Width - x1;

				for (; i < tileMaxY; i++)
				{
					var row = _pixels.DangerousGetRowSpan(i);
					row[..x0].Fill(_color);
					row.Slice(x1, w1).Fill(_color);
				}
			}
		}
	}

	private readonly struct UncroppedCircleRowIntervalOperation : IRowIntervalOperation
	{
		private readonly Buffer2D<TPixel> _pixels;
		private readonly TPixel _color;
		private readonly int _maxX;
		private readonly int _midPointY;

		public UncroppedCircleRowIntervalOperation(Buffer2D<TPixel> pixels, TPixel color)
		{
			_pixels = pixels;
			_color = color;
			_maxX = _pixels.Width - 1;
			_midPointY = _pixels.Height >>> 1;
		}

		public void Invoke(in RowInterval rows)
		{
			for (int i = rows.Min; i < rows.Max; i++)
			{
				int y;
				if (i < _midPointY) y = i + 1;
				else if (i > _midPointY) y = _pixels.Height - i;
				else y = i;

				int x = (_pixels.Width - (int)Math.Floor(Math.Sqrt((y * (_pixels.Height - y)) << 2))) >> 1;

				var row = _pixels.DangerousGetRowSpan(i);
				row[..x].Fill(_color);
				row.Slice(_pixels.Width - x, x).Fill(_color);
			}
		}
	}
}
