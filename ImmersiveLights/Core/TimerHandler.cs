using System;
using System.Threading;

namespace ImmersiveLights.Core
{
    public class TimerHandler
    {
        #region Variables
        private Thread thread;
        private bool isRunning;
        private bool isCancellationRequested;
        private Action createAction;
        private Action updateAction;
        private Action cancelAction;
        private int interval;
        #endregion

        #region Main
        public TimerHandler()
        {
            this.interval = 100;
        }
        #endregion

        #region Controls
        /// <summary>
        /// Crea il timer.
        /// </summary>
        public void Create(Action createAction, Action updateAction)
        {
            this.createAction = createAction;
            this.updateAction = updateAction;

            thread = new Thread(OnUpdate);
        }
        /// <summary>
        /// Avvia il timer.
        /// </summary>
        public void Start()
        {
            // Annulla la funzione se il thread non esiste.
            if (isRunning) return;

            // Avvia il thread.
            isRunning = true;
            isCancellationRequested = false;
            thread.Start();
        }

        private void OnUpdate()
        {
            createAction();

            while (!isCancellationRequested)
            {
                updateAction();
                Thread.Sleep(interval);
            }

            cancelAction();
        }
        /// <summary>
        /// Arresta il timer
        /// </summary>
        public void Stop(Action cancelAction)
        {
            // Annulla la funzione se il thread non esiste.
            if (!isRunning) return;

            this.cancelAction = cancelAction;

            isRunning = false;
            isCancellationRequested = true;
        }
        /// <summary>
        /// Imposta un nuovo intervallo al timer.
        /// </summary>
        public void SetInterval(int interval)
        {
            this.interval = interval;
        }
        #endregion
    }
}
