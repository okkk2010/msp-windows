using System;
using msp_windows.Overlay.Models;

namespace msp_windows.Overlay
{
    public class OverlayApplyService
    {
        public OverlayDocument Current { get; private set; }

        public event Action<OverlayDocument> OnApplied;

        public void Apply(OverlayDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            Current = document;
            OverlayForm.CurrentOverlayDocument = document;
            OverlayForm.Instance?.Invalidate();

            OnApplied?.Invoke(document);
        }

        public void Clear()
        {
            Current = null;
            OverlayForm.CurrentOverlayDocument = null;
            OverlayForm.Instance?.Invalidate();
        }
    }
}
