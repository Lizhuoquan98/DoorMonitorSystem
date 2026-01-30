
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.system
{
    /// <summary>
    /// 图形字典分组表 (对应 GraphicDictionary.json 的 Key)
    /// </summary>
    public class SysGraphicGroupEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 图形组名称 (唯一键)
        /// </summary>
        public string GroupName { get; set; }
    }

    /// <summary>
    /// 图形图元项表 (对应 GraphicDictionary.json 的 Value List)
    /// </summary>
    public class SysGraphicItemEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 关联的分组 ID (Foreign Key -> SysGraphicGroupEntity.Id)
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// SVG 路径数据
        /// </summary>
        public string PathData { get; set; }

        /// <summary>
        /// 填充颜色 (#RRGGBB or Name)
        /// </summary>
        public string FillColor { get; set; }

        /// <summary>
        /// 描边颜色
        /// </summary>
        public string StrokeColor { get; set; }

        /// <summary>
        /// 线宽
        /// </summary>
        public double StrokeThickness { get; set; }

        /// <summary>
        /// 排序索引 (确保加载顺序一致)
        /// </summary>
        public int SortIndex { get; set; }
    }
}
