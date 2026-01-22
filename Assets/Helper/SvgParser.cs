using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// SVG 文件解析器
    /// 支持从 SVG 文件中提取 Path 数据、填充色、描边色等信息
    /// </summary>
    public class SvgParser
    {
        /// <summary>
        /// SVG Path 项
        /// </summary>
        public class SvgPathItem
        {
            public string PathData { get; set; } = "";
            public string Fill { get; set; } = "#000000";
            public string Stroke { get; set; } = "none";
            public double StrokeThickness { get; set; } = 1;
            public string Transform { get; set; } = "";
        }

        /// <summary>
        /// 解析 SVG 文件，提取所有 Path 元素
        /// </summary>
        public static List<SvgPathItem> ParseSvgFile(string svgContent)
        {
            var pathItems = new List<SvgPathItem>();

            try
            {
                // 解析 XML（XDocument.Parse 可以自动处理 XML 声明）
                var doc = XDocument.Parse(svgContent);
                XNamespace ns = "http://www.w3.org/2000/svg";

                // 提取所有 <path> 元素
                var paths = doc.Descendants(ns + "path");

                foreach (var path in paths)
                {
                    var item = new SvgPathItem();

                    // 提取 d 属性（Path Data）
                    item.PathData = path.Attribute("d")?.Value ?? "";

                    // 提取 fill 属性
                    item.Fill = path.Attribute("fill")?.Value ?? "none";

                    // 提取 stroke 属性
                    item.Stroke = path.Attribute("stroke")?.Value ?? "none";

                    // 提取 stroke-width 属性
                    var strokeWidth = path.Attribute("stroke-width")?.Value;
                    if (!string.IsNullOrEmpty(strokeWidth))
                    {
                        if (double.TryParse(strokeWidth, out double width))
                        {
                            item.StrokeThickness = width;
                        }
                    }

                    // 提取 transform 属性
                    item.Transform = path.Attribute("transform")?.Value ?? "";

                    // 只添加有效的 Path
                    if (!string.IsNullOrEmpty(item.PathData))
                    {
                        pathItems.Add(item);
                    }
                }

                // 如果没有找到 path，尝试查找没有命名空间的 path
                if (pathItems.Count == 0)
                {
                    paths = doc.Descendants("path");
                    foreach (var path in paths)
                    {
                        var item = new SvgPathItem();
                        item.PathData = path.Attribute("d")?.Value ?? "";
                        item.Fill = path.Attribute("fill")?.Value ?? "none";
                        item.Stroke = path.Attribute("stroke")?.Value ?? "none";

                        var strokeWidth = path.Attribute("stroke-width")?.Value;
                        if (!string.IsNullOrEmpty(strokeWidth) && double.TryParse(strokeWidth, out double width))
                        {
                            item.StrokeThickness = width;
                        }

                        item.Transform = path.Attribute("transform")?.Value ?? "";

                        if (!string.IsNullOrEmpty(item.PathData))
                        {
                            pathItems.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"SVG 解析失败: {ex.Message}", ex);
            }

            return pathItems;
        }

        /// <summary>
        /// 应用 Transform 变换到 Path Data
        /// 支持 translate(x, y) 变换
        /// </summary>
        public static string ApplyTransform(string pathData, string transform)
        {
            if (string.IsNullOrEmpty(transform))
                return pathData;

            // 解析 translate(x, y)
            var translateMatch = Regex.Match(transform, @"translate\(([^,]+),?\s*([^)]*)\)");
            if (translateMatch.Success)
            {
                double tx = 0, ty = 0;

                if (double.TryParse(translateMatch.Groups[1].Value.Trim(), out double x))
                    tx = x;

                if (translateMatch.Groups[2].Success &&
                    double.TryParse(translateMatch.Groups[2].Value.Trim(), out double y))
                    ty = y;

                // 简单处理：在 Path Data 前添加 translate 指令
                // 更复杂的变换需要完整的 SVG 变换矩阵计算
                return $"M {tx},{ty} " + pathData;
            }

            return pathData;
        }

        /// <summary>
        /// 规范化 Path Data（去除多余空格、格式化）
        /// </summary>
        public static string NormalizePathData(string pathData)
        {
            if (string.IsNullOrEmpty(pathData))
                return "";

            // 移除多余的空格
            pathData = Regex.Replace(pathData, @"\s+", " ");

            // 移除逗号前后的空格
            pathData = Regex.Replace(pathData, @"\s*,\s*", ",");

            return pathData.Trim();
        }

        /// <summary>
        /// 将 SVG 颜色转换为 WPF Brush
        /// </summary>
        public static Brush ParseColorToBrush(string color)
        {
            if (string.IsNullOrEmpty(color) || color.ToLower() == "none")
                return Brushes.Transparent;

            try
            {
                // 处理十六进制颜色
                if (color.StartsWith("#"))
                {
                    var colorValue = (Color)ColorConverter.ConvertFromString(color);
                    return new SolidColorBrush(colorValue);
                }

                // 处理命名颜色
                var namedColor = (Color)ColorConverter.ConvertFromString(color);
                return new SolidColorBrush(namedColor);
            }
            catch
            {
                return Brushes.Black;
            }
        }

        /// <summary>
        /// 将几何路径归一化到原点 (0,0)
        /// 保持原始形状和宽高比，只移动位置
        /// </summary>
        public static Geometry NormalizePathToOrigin(Geometry geometry)
        {
            if (geometry == null)
                return null;

            try
            {
                // 获取几何图形的边界框
                var bounds = geometry.Bounds;

                // 如果边界框为空或无效，直接返回原始几何
                if (bounds.IsEmpty || double.IsNaN(bounds.X) || double.IsNaN(bounds.Y))
                    return geometry;

                // 计算需要的偏移量，将图形移到原点
                double offsetX = -bounds.X;
                double offsetY = -bounds.Y;

                // 创建平移变换
                var transform = new TranslateTransform(offsetX, offsetY);

                // 应用变换并返回新的几何图形
                var transformedGeometry = geometry.Clone();
                transformedGeometry.Transform = transform;

                // 将变换后的几何图形"冻结"为新的路径数据
                var pathGeometry = PathGeometry.CreateFromGeometry(transformedGeometry);
                
                return pathGeometry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"归一化路径失败: {ex.Message}");
                return geometry; // 出错时返回原始几何
            }
        }

        /// <summary>
        /// 获取几何图形的边界信息（用于调试和日志）
        /// </summary>
        public static string GetGeometryBoundsInfo(Geometry geometry)
        {
            if (geometry == null)
                return "null";

            var bounds = geometry.Bounds;
            if (bounds.IsEmpty)
                return "Empty";

            return $"X={bounds.X:F2}, Y={bounds.Y:F2}, W={bounds.Width:F2}, H={bounds.Height:F2}";
        }
    }
}
