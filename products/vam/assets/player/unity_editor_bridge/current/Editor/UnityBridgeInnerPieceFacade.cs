namespace FrameAngel.UnityEditorBridge
{
    public static class UnityBridgeInnerPieceFacade
    {
        public static string InnerPieceExportRoot => UnityBridgeController.InnerPieceExportRoot;

        public static UnityInnerPieceLastExportSummary LastInnerPieceExport => UnityBridgeController.LastInnerPieceExport;

        public static UnityBridgeResponse ExportSelectionFromWindow(
            string displayNameOverride,
            string outputPath,
            bool capturePreview,
            string tagsCsv)
        {
            return UnityBridgeInnerPieceService.ExportSelectionFromWindow(
                displayNameOverride,
                outputPath,
                capturePreview,
                tagsCsv);
        }
    }
}
