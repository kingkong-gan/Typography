//Apache2, 2014-present, WinterDev

using System;
using System.Collections.Generic;
using Typography.Text;

namespace LayoutFarm.TextEditing
{
    public readonly struct VisualSelectionRangeSnapShot
    {
        public readonly int startLineNum;
        public readonly int startColumnNum;
        public readonly int endLineNum;
        public readonly int endColumnNum;
        public VisualSelectionRangeSnapShot(int startLineNum, int startColumnNum, int endLineNum, int endColumnNum)
        {
            this.startLineNum = startLineNum;
            this.startColumnNum = startColumnNum;
            this.endLineNum = endLineNum;
            this.endColumnNum = endColumnNum;
        }
        public bool IsEmpty()
        {
            return startLineNum == 0 && startColumnNum == 0
                && endLineNum == 0 && endColumnNum == 0;
        }
        public static readonly VisualSelectionRangeSnapShot Empty = new VisualSelectionRangeSnapShot();
    }
}