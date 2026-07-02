using UnityEngine;

namespace ETD.Hub
{

    // =========================================================================
    // SHOP SLOT
    // =========================================================================
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private GameObject _purchasedImage;

        public void SetPurchased(bool purchased)
        {
            if (_purchasedImage != null) _purchasedImage.SetActive(purchased);
        }
    }
}
