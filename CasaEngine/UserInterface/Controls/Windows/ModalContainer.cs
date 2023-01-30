
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public abstract class ModalContainer : Container
    {


        // It stores the previous modal when this modal is activated.
        private ModalContainer lastModal;

        // Default value.
        private ModalResult modalResult = ModalResult.None;



        public override bool Visible
        {
            get => base.Visible;
            set
            {
                if (value)
                    Focused = true;
                base.Visible = value;
            }
        } // Visible

        public virtual bool IsTheActiveModal => UserInterfaceManager.ModalWindow == this;

        public virtual ModalResult ModalResult
        {
            get => modalResult;
            set => modalResult = value;
        } // ModalResult



        public event WindowClosingEventHandler Closing;
        public event WindowClosedEventHandler Closed;



        protected ModalContainer(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {

        }



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            Closing = null;
            Closed = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        public virtual void ShowModal()
        {
            // You can't activate the modal mode twice at the same time.
            if (UserInterfaceManager.ModalWindow == this)
                return;
            lastModal = UserInterfaceManager.ModalWindow;
            UserInterfaceManager.ModalWindow = this;
            // This allow to close the modal window with the escape key.
            UserInterfaceManager.InputSystem.KeyDown += InputKeyDown;
        } // ShowModal



        public virtual void Close()
        {
            // Raise on closing event.
            WindowClosingEventArgs ex = new WindowClosingEventArgs();
            OnClosing(ex);
            // If an event does not deny the closing...
            if (!ex.Cancel)
            {
                // Remove the event link to prevent garbage.
                UserInterfaceManager.InputSystem.KeyDown -= InputKeyDown;
                // Restore previous modal window.
                UserInterfaceManager.ModalWindow = lastModal;
                if (lastModal != null)
                    lastModal.Focused = true;
                else
                    UserInterfaceManager.FocusedControl = null;
                Hide();
                // Raise on closed event.
                WindowClosedEventArgs ev = new WindowClosedEventArgs();
                OnClosed(ev);
                // If an event does not change the dispose property.
                if (ev.Dispose)
                    Dispose();
            }
        } // Close

        public virtual void Close(ModalResult modalResult)
        {
            ModalResult = modalResult;
            Close();
        } // Close



        protected virtual void OnClosing(WindowClosingEventArgs e)
        {
            if (Closing != null)
                Closing.Invoke(this, e);
        } // OnClosing

        protected virtual void OnClosed(WindowClosedEventArgs e)
        {
            if (Closed != null)
                Closed.Invoke(this, e);
        } // OnClosed



        void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (Visible && UserInterfaceManager.FocusedControl == this && e.Key == Microsoft.Xna.Framework.Input.Keys.Escape)
            {
                Close(ModalResult.Cancel);
            }
        } // InputKeyDown


    } // ModalContainer
} // XNAFinalEngine.UserInterface