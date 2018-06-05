using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs4Net
{
	public sealed class ListFormat : IFormat<SecsItem>
	{
		public const SecsFormat Format = SecsFormat.List;

		private static readonly Pool<ListItem> ListItemPool
			= new Pool<ListItem>(p => new ListItem(p));

		public static readonly SecsItem Empty = new ListItem();

		/// <summary>
		/// Create ListItem
		/// </summary>
		/// <param name="value">dynamic allocated item collection</param>
		/// <returns></returns>
		public static SecsItem Create(IEnumerable<SecsItem> value)
		{
			var secsItems = value as SecsItem[] ?? value.ToArray();
			return secsItems.Length == 0 ? Empty : Create(secsItems);
		}

		/// <summary>
		/// Create ListItem
		/// </summary>
		/// <param name="value">dynamic allocated item array</param>
		/// <returns></returns>
		public static SecsItem Create(SecsItem[] value)
		{
			var listItem = ListItemPool.Rent();
			listItem.SetItems(new ArraySegment<SecsItem>(value), fromPool: false);
			return listItem;
		}

		/// <summary>
		/// Create PooledListItem
		/// </summary>
		/// <param name="value">item list from pool</param>
		/// <returns></returns>
		internal static SecsItem Create(ArraySegment<SecsItem> value)
		{
			var listItem = ListItemPool.Rent();
			listItem.SetItems(value, fromPool: true);
			return listItem;
		}
	}
}
