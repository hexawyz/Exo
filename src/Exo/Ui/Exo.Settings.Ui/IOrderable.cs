namespace Exo.Settings.Ui;

internal interface IOrderable
{
	uint DisplayOrder { get; }

	public static int FindInsertPosition(IReadOnlyList<IOrderable> items, uint displayOrder)
	{
		if (items.Count == 0) return 0;

		int min = 0;
		int max = items.Count - 1;

		if (items[min].DisplayOrder > displayOrder) return min;
		if (items[max].DisplayOrder <= displayOrder) return items.Count;

		while (max > min)
		{
			int med = (min + max) >> 1;
			uint order = items[med].DisplayOrder;
			if (order > displayOrder) max = med - 1;
			else min = med + 1;
		}
		return items[min].DisplayOrder == displayOrder ? min + 1 : min;
	}
}
