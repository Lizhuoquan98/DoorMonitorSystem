using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DoorMonitorSystem.Assets.Helper
{
    public static class PerformanceHelper
    {
        public static bool IsLowGraphicsMode { get; private set; }

        public static void Initialize()
        {
            // Tier < 2 通常意味着缺少 GPU 加速
            IsLowGraphicsMode = RenderCapability.Tier < 0x20000;

            var v = Environment.OSVersion.Version;
            // Win7 及以下默认进入低图形模式
            if (v.Major < 6 || (v.Major == 6 && v.Minor <= 1))
            {
                IsLowGraphicsMode = true;
            }
        }

        public static void ApplyLowGraphicsMode(DependencyObject root)
        {
            if (!IsLowGraphicsMode || root == null) return;

            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current is UIElement ui && ui.Effect != null)
                {
                    ui.Effect = null;
                }

                if (current is FrameworkElement fe)
                {
                    ToolTipService.SetIsEnabled(fe, false);
                }

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; i++)
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(current, i));
                }
            }
        }
    }
}
