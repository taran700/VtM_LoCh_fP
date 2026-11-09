using VtM_LoCh_fP.ViewModels;

static void Check(bool condition, string behavior)
{
    if (!condition) throw new Exception("FAILED: " + behavior);
}

static void AssertEqual(int expected, int actual, string behavior)
{
    if (expected != actual) throw new Exception($"{behavior}: expected {expected}, got {actual}");
}

// ===== Точки: клик по пустой заполняет её и всё левее =====
AssertEqual(4, TrackLogic.ValueAfterClick(0, 3), "clicking an empty fourth dot fills it and all dots left of it");
AssertEqual(1, TrackLogic.ValueAfterClick(0, 0), "clicking the first empty dot fills exactly one");
AssertEqual(5, TrackLogic.ValueAfterClick(2, 4), "clicking the fifth empty dot fills through the fifth");

// ===== Точки: клик по закрашенной оставляет её и очищает всё правее =====
AssertEqual(3, TrackLogic.ValueAfterClick(5, 2), "clicking a filled middle dot keeps it and clears dots to the right");
AssertEqual(1, TrackLogic.ValueAfterClick(4, 0), "clicking the first filled dot keeps it and clears all dots right of it");
AssertEqual(3, TrackLogic.ValueAfterClick(4, 3), "clicking the last filled dot removes that dot");
AssertEqual(0, TrackLogic.ValueAfterClick(1, 0), "clicking the only filled dot removes it");

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
try { TrackLogic.ValueAfterClick(0, -1); throw new Exception("FAILED: negative index must throw"); }
catch (ArgumentOutOfRangeException) { }
try { TrackLogic.NextHealthState(3); throw new Exception("FAILED: state above two must throw"); }
catch (ArgumentOutOfRangeException) { }
try { TrackLogic.Move(tabs, 9, 0); throw new Exception("FAILED: index outside the list must throw"); }
catch (ArgumentOutOfRangeException) { }

Console.WriteLine("All TrackLogic checks passed.");
