using System;
using System.Collections.Generic;

namespace VtM_LoCh_fP.ViewModels
{
    /// <summary>
    /// Логика дорожек значений. Вынесена из вьюх, чтобы её можно было
    /// проверить тестами без запуска окна.
    /// </summary>
    public static class TrackLogic
    {
        /// <summary>
        /// Отметка точки: отмеченная точка закрашивает себя и все левее,
        /// снятая — очищает себя и все правее.
        /// </summary>
        internal static int ValueAfterToggle(int zeroBasedIndex, bool isChecked)
        {
            if (zeroBasedIndex < 0) throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
            return isChecked ? zeroBasedIndex + 1 : zeroBasedIndex;
        }

        /// <summary>Цикл отметки здоровья: 0 → 1 палка → 2 палки (крест) → 0.</summary>
        internal static int NextHealthState(int currentState)
        {
            if (currentState < 0 || currentState > 2) throw new ArgumentOutOfRangeException(nameof(currentState));
            return (currentState + 1) % 3;
        }

        internal static void Move<T>(IList<T> items, int sourceIndex, int targetIndex)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (sourceIndex < 0 || sourceIndex >= items.Count) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (targetIndex < 0 || targetIndex >= items.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));

            if (sourceIndex == targetIndex) return;
            T item = items[sourceIndex];
            items.RemoveAt(sourceIndex);
            items.Insert(targetIndex, item);
        }

        /// <summary>Индекс вкладки, перед которой надо вставить перетаскиваемую.</summary>
        internal static int TargetIndex(double pointerX, IList<double> centers)
        {
            if (centers == null) throw new ArgumentNullException(nameof(centers));
            for (int i = 0; i < centers.Count; i++)
                if (pointerX < centers[i]) return i;
            return Math.Max(0, centers.Count - 1);
        }
    }
}
