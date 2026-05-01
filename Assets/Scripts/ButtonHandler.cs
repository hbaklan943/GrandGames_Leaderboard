using UnityEngine;

public class ButtonHandler : MonoBehaviour
{
    public LeaderboardManager leaderboardManager;
    public Camera uiCamera;

    void Start()
    {
        // Fallback to main camera if not assigned
        if (uiCamera == null)
            uiCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Input.mousePosition;
            Ray ray = uiCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform == transform ||
                    hit.transform.IsChildOf(transform))
                {
                    leaderboardManager.OnUpdateClicked();
                }
            }
        }
    }
}