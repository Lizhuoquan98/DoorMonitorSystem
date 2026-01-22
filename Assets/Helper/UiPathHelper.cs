

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using DoorMonitorSystem.Models;
using System.Windows.Controls;

namespace DoorMonitorSystem.Assets.Helper
{
    // 控件路径工具类
    public static class UiPathHelper
    {
        /// <summary>
        /// 遍历整个控件树，返回所有带 Tag 的控件，构成完整路径树
        /// </summary>
        /// <param name="root">起始 DependencyObject</param>
        /// <returns>带路径的 PathDirectory 树（可能有多个根）</returns>
        public static List<PathDirectory> GeneratePathDirectoryTree(DependencyObject root)
        {
            var result = new List<PathDirectory>();
            TraverseControls(root, null, result, null);
            return result;
        }

        /// <summary>
        /// 遍历 UI 控件树，构建 PathDirectory 树
        /// </summary>
        /// <param name="parent">当前控件</param>
        /// <param name="parentPath">父节点完整路径</param>
        /// <param name="container">当前列表容器</param>
        /// <param name="parentNode">父节点对象</param>
        private static void TraverseControls(DependencyObject parent, string? parentPath, List<PathDirectory> container, PathDirectory? parentNode)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element)
                {
                    // 有 Tag 的控件我们才创建 PathDirectory 节点
                    if (element.Tag != null)
                    {
                        string pathName = element.Tag.ToString()!;
                        string fullPath = string.IsNullOrEmpty(parentPath) ? pathName : $"{parentPath}.{pathName}";

                        var node = new PathDirectory
                        {
                            PathName = pathName,
                            FullPath = fullPath,
                            Paths = new List<PathDirectory>()
                        };

                        // 添加到父容器中
                        if (parentNode != null)
                            parentNode.Paths!.Add(node);
                        else
                            container.Add(node);

                        // 继续递归子节点
                        TraverseControls(child, fullPath, container, node);

                        // 如果子节点为空，设为 null（优化结构）
                        if (node.Paths!.Count == 0)
                            node.Paths = null;
                    }
                    else
                    {
                        // 如果当前控件没有 Tag，就继续找子控件
                        TraverseControls(child, parentPath, container, parentNode);
                    }


                  

                }
            }
        }
    }

 }
