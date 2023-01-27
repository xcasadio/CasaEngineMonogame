using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngineCommon.Helper
{
	public static class GeometryHelper
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="point_"></param>
		/// <param name="rect_"></param>
		/// <returns></returns>
		static public bool PointInsideRect(Point point_, Rectangle rect_)
		{
			if (point_.X >= rect_.Left && point_.X <= rect_.Right &&
				point_.Y >= rect_.Top && point_.Y <= rect_.Bottom)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rect1_"></param>
		/// <param name="rect2_"></param>
		/// <returns></returns>
		static public bool RectCollideRect(Rectangle rect1_, Rectangle rect2_)
		{
			if (RectInsideRect(rect1_, rect2_) == false)
			{
				return RectInsideRect(rect2_, rect1_);
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rect1_"></param>
		/// <param name="rect2_"></param>
		/// <returns></returns>
		static public bool RectInsideRect(Rectangle rect1_, Rectangle rect2_)
		{
			Point point = new Point();

			point.X = rect1_.Left;
			point.Y = rect1_.Top;

			if (PointInsideRect(point, rect2_) == true)
			{
				return true;
			}

			point.X = rect1_.Right;
			point.Y = rect1_.Top;

			if (PointInsideRect(point, rect2_) == true)
			{
				return true;
			}

			point.X = rect1_.Left;
			point.Y = rect1_.Bottom;

			if (PointInsideRect(point, rect2_) == true)
			{
				return true;
			}

			point.X = rect1_.Right;
			point.Y = rect1_.Bottom;

			if (PointInsideRect(point, rect2_) == true)
			{
				return true;
			}

			return false;
		}
	}
}
