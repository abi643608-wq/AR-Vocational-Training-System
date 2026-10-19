using UnityEngine;

/// <summary>Visual and collider state of one extinguisher; no AR or training logic.</summary>
[DisallowMultipleComponent]
public sealed class SIHExtinguisher : MonoBehaviour
{
    [SerializeField] private Collider selectionCollider;
    [SerializeField] private GameObject selectionHighlight;
    public bool IsSelected { get; private set; }
    public Collider SelectionCollider => selectionCollider;
    private TMPro.TMP_Text trainingProfile;
    public void SetTrainingProfile(string label)
    {
        if(trainingProfile==null){var go=new GameObject("Selected training profile");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.67f,0);trainingProfile=go.AddComponent<TMPro.TextMeshPro>();trainingProfile.font=TMPro.TMP_Settings.defaultFontAsset;trainingProfile.fontSize=.85f;trainingProfile.alignment=TMPro.TextAlignmentOptions.Center;trainingProfile.rectTransform.sizeDelta=new Vector2(.7f,.3f);}
        trainingProfile.text=label+"\nPOWDER SIMULATION";
        if(Camera.main!=null)trainingProfile.transform.rotation=Quaternion.LookRotation(trainingProfile.transform.position-Camera.main.transform.position,Vector3.up);
    }

    private void Awake() => SetSelected(false);

    public void Configure(Collider target, GameObject highlight)
    {
        selectionCollider = target;
        selectionHighlight = highlight;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    public bool HitTest(Ray ray)
    {
        return selectionCollider != null && selectionCollider.enabled &&
               selectionCollider.gameObject.activeInHierarchy &&
               selectionCollider.Raycast(ray, out _, 20f);
    }
}
