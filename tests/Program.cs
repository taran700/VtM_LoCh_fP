using VtM_LoCh_fP.ViewModels;

static void Check(bool condition, string behavior)
{
    if (!condition) throw new Exception("FAILED: " + behavior);
}

static void AssertEqual(int expected, int actual, string behavior)
{
    if (expected != actual) throw new Exception($"{behavior}: expected {expected}, got {actual}");
}

// ===== Точки: отметка закрашивает себя и всё левее =====
AssertEqual(4, TrackLogic.ValueAfterToggle(3, true), "checking a dot selects it and all dots left of it");
AssertEqual(1, TrackLogic.ValueAfterToggle(0, true), "checking the first dot selects exactly one");
AssertEqual(5, TrackLogic.ValueAfterToggle(4, true), "checking the last of five selects all five");

// ===== Точки: снятие очищает себя и всё правее =====
AssertEqual(2, TrackLogic.ValueAfterToggle(2, false), "unchecking a dot clears it and all dots to its right");
AssertEqual(0, TrackLogic.ValueAfterToggle(0, false), "unchecking the first dot clears the whole track");
AssertEqual(4, TrackLogic.ValueAfterToggle(4, false), "unchecking the last of five leaves four");

// ===== Запас крови: 10 квадратов, отметка каскадная =====
AssertEqual(10, TrackLogic.ValueAfterToggle(9, true), "checking the tenth blood square fills the whole pool");
AssertEqual(7, TrackLogic.ValueAfterToggle(6, true), "checking the seventh blood square fills seven");
AssertEqual(3, TrackLogic.ValueAfterToggle(3, false), "unchecking the fourth blood square leaves three");
AssertEqual(0, TrackLogic.ValueAfterToggle(0, false), "draining the pool from the middle clears to zero");

// ===== Здоровье: пусто → одна палка → две палки → пусто =====
AssertEqual(1, TrackLogic.NextHealthState(0), "first health click draws one slash");
AssertEqual(2, TrackLogic.NextHealthState(1), "second health click draws two slashes");
AssertEqual(0, TrackLogic.NextHealthState(2), "third health click clears the box");

// ===== Перестановка вкладок =====
var tabs = new List<string> { "Лист", "Дисциплины", "Заметки" };
TrackLogic.Move(tabs, 0, 2);
Check(string.Join(",", tabs) == "Дисциплины,Заметки,Лист",
    "dragging the first tab onto the last moves it to the end");

// ===== Куда встанет вкладка при перетаскивании =====
var centers = new List<double> { 100, 300, 500 };
AssertEqual(0, TrackLogic.TargetIndex(50, centers), "pointer left of the first tab targets slot zero");
AssertEqual(1, TrackLogic.TargetIndex(200, centers), "pointer past the first centre targets slot one");
AssertEqual(2, TrackLogic.TargetIndex(900, centers), "pointer past the last centre targets the last slot");

// ===== Ошибки в аргументах должны быть видны =====
try { TrackLogic.ValueAfterToggle(-1, true); throw new Exception("FAILED: negative index must throw"); }
catch (ArgumentOutOfRangeException) { }
try { TrackLogic.NextHealthState(3); throw new Exception("FAILED: state above two must throw"); }
catch (ArgumentOutOfRangeException) { }
try { TrackLogic.Move(tabs, 9, 0); throw new Exception("FAILED: index outside the list must throw"); }
catch (ArgumentOutOfRangeException) { }

Console.WriteLine("All TrackLogic checks passed.");
