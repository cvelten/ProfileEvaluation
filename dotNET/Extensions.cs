using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfileEvaluation
{
	public static class Extensions
	{
		public static int FindIndexOfMin(this IEnumerable<double> @this, bool absolute = false)
		{
			if (absolute)
			{
				var absMin = @this.Select(x => Math.Abs(x)).Min();
				return @this
					.Select((v, i) => new { v, i })
					.FirstOrDefault(x => Math.Abs(x.v) == absMin)?.i ?? -1;
			}
			else
			{
				var min = @this.Min();
				return @this
					.Select((v, i) => new { v, i })
					.FirstOrDefault(x => x.v == min)?.i ?? -1;
			}
		}

		public static int FindIndexOfMax(this IEnumerable<double> @this, bool absolute = false)
		{
			if (absolute)
			{
				var absMin = @this.Select(x => Math.Abs(x)).Max();
				return @this
					.Select((v, i) => new { v, i })
					.FirstOrDefault(x => Math.Abs(x.v) == absMin)?.i ?? -1;
			}
			else
			{
				var min = @this.Max();
				return @this
					.Select((v, i) => new { v, i })
					.FirstOrDefault(x => x.v == min)?.i ?? -1;
			}
		}
	}
}
