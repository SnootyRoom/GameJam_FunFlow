namespace CatTanks
{
    public enum GameState
    {
        Menu,       // титульный экран поверх уровня 1
        Loading,    // уровень собирается
        Playing,    // игра идёт
        Paused,     // Esc
        LevelWon,   // кот у рыбки: экран победы + магазин прокачки
        LevelLost,  // HP = 0 или кончилось время
    }

    public enum LoseReason
    {
        Killed,
        TimeOut,
    }
}
