using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemAdder.UI
{
    /// <summary>
    /// Ajoute un bouton « Sauvegarder et menu » dans le menu Échap.
    ///
    /// On clone le bouton « Oui » de la confirmation « Retour au menu
    /// principal » : son FSM PlayMaker exécute déjà tout le flux du jeu
    /// (sauvegarde, fermeture du lobby, arrêt de l'hôte et retour au menu).
    /// Le bouton est placé entre « Sauvegarder et quitter » et « Retour ».
    /// </summary>
    internal sealed class EscapeMenuSaveButton : MonoBehaviour
    {
        private const string ButtonName = "ItemAdder_SaveAndMenuButton";
        private bool _added;

        private void Update()
        {
            if (_added)
                return;

            GameObject master = GameObject.Find("MasterOBJ");
            if (master == null)
                return;

            Transform menus = master.transform.Find("MasterCanvas/Menus");
            if (menus == null)
                return;

            Transform escapeMenu = menus.Find("EscapeMenu");
            Transform yesButton = menus.Find("MainMenu_AreYouSure/YesButton");
            if (escapeMenu == null || yesButton == null)
                return;

            GameObject button = Object.Instantiate(yesButton.gameObject, escapeMenu);
            button.name = ButtonName;
            button.SetActive(true);

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -125f);
                rect.sizeDelta = new Vector2(300f, 65f);
            }

            // Le bouton d'origine affiche « Oui » : on le renomme une seule fois.
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                text.text = "Sauvegarder et menu";

            Plugin.Log.LogInfo("[Interface] Bouton « Sauvegarder et menu » ajouté au menu Échap.");
            _added = true;
        }
    }
}
