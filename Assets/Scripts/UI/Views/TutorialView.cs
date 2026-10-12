// Обучение: карточки поверх всего UI (How to play → Ultimate → Your goal). Лежит на корне префаба Tutorial.
// Листает карточки само; кто открыл — тот получает Closed и решает, что дальше (вернуться в меню, начать игру…).
using System;
using UnityEngine;

namespace CatTanks.UI
{
    public class TutorialView : MonoBehaviour
    {
        [Tooltip("Карточки по порядку")]
        public GameObject[] cards;
        [Tooltip("NEXT на каждой карточке, кроме последней")]
        public PixelButton[] next;
        public PixelButton skip;
        [Tooltip("Кнопка на последней карточке")]
        public PixelButton go;

        public event Action Closed;

        int _index;

        void Awake()
        {
            // подписка кодом в префаб не сохраняется — назначаем при каждом создании
            foreach (var b in next) if (b != null) b.onActivated.AddListener(() => Show(_index + 1));
            if (skip != null) skip.onActivated.AddListener(() => Show(2));
            if (go != null) go.onActivated.AddListener(Close);
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open()
        {
            gameObject.SetActive(true);
            Show(0);
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Show(int index)
        {
            if (index >= cards.Length) { Close(); return; }
            _index = index;
            for (int i = 0; i < cards.Length; i++) cards[i].SetActive(i == index);
        }
    }
}
