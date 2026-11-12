using System;
using System.Windows;
using System.Reflection;
using Newtonsoft.Json;

namespace VtM_LoCh_fP
{
    /// <summary>Однослотовая корзина: хранит только последнее отменяемое удаление.</summary>
    internal static class UndoService
    {
        private static Action _restore;
        private static string _description;
        public static bool IsRestoring { get; private set; }

        public static string CaptureCreationState(CharacterProfile character)
        {
            return character == null || character.CreationState == null ? null : JsonConvert.SerializeObject(character.CreationState);
        }

        public static void RestoreCreationState(CharacterProfile character, string snapshot)
        {
            if (character == null || string.IsNullOrWhiteSpace(snapshot)) return;
            var restored = JsonConvert.DeserializeObject<CharacterCreationState>(snapshot);
            if (restored == null) return;
            var target = character.CreationState ?? (character.CreationState = new CharacterCreationState());
            foreach (var property in typeof(CharacterCreationState).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0) continue;
                if (property.Name == "FreebiePoints" || property.Name == "FreebiePurchasesConfirmed") continue;
                property.SetValue(target, property.GetValue(restored, null), null);
            }
            target.FreebiePoints = restored.FreebiePoints;
            target.FreebiePurchasesConfirmed = restored.FreebiePurchasesConfirmed;
        }

        public static bool CanUndo { get { return _restore != null; } }
        public static string Description { get { return _description ?? string.Empty; } }

        public static void Store(string description, Action restore)
        {
            if (IsRestoring || restore == null) return;
            _description = string.IsNullOrWhiteSpace(description) ? "удалённый элемент" : description;
            _restore = restore;
        }

        public static bool TryUndo(Window owner)
        {
            Action restore = _restore;
            string description = _description;
            if (restore == null) return false;

            // Сначала очищаем слот: повторный вызов из восстановительного кода
            // не должен выполнить одно и то же действие дважды.
            _restore = null;
            _description = null;
            try
            {
                IsRestoring = true;
                restore();
                if (owner != null)
                    ThemedDialog.Show(owner, "Восстановлено: " + description + ".", "Отмена действия",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Undo failed: " + ex);
                if (owner != null)
                    ThemedDialog.Show(owner, "Не удалось восстановить «" + description + "».\n" + ex.Message,
                        "Ошибка отмены", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally { IsRestoring = false; }
        }
    }
}
