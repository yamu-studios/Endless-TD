// ============================================================================
// ETD.UI.Wiki - WikiIconBinder.cs
// Hands the stat glyphs to WikiStatIcons at runtime.
//
// Wiki pages are plain C# objects created by WikiCatalog, so they have no
// serialized fields of their own to reference sprites from. AssetDatabase covers
// the editor but does not exist in a build, so this component — sitting on the
// wiki window — carries the references and pushes them across before any page is
// built.
// ============================================================================
using UnityEngine;

namespace ETD.UI.Wiki
{
    [DisallowMultipleComponent]
    public class WikiIconBinder : MonoBehaviour
    {
        [Header("Stat Icons")]
        [SerializeField] private Sprite _damageIcon;
        [SerializeField] private Sprite _rangeIcon;
        [SerializeField] private Sprite _speedIcon;

        // Awake rather than OnEnable: the window rebuilds its pages in OnEnable, and the
        // sprites have to be in place before the first page is built.
        private void Awake()
        {
            WikiStatIcons.Bind(_damageIcon, _rangeIcon, _speedIcon);
        }
    }
}
