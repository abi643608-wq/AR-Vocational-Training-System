using TMPro;
using UnityEngine;

/// <summary>Session-local training sign, independent of real doors or obstacles.</summary>
public sealed class SIHRouteMarker : MonoBehaviour
{
    public Collider SelectionCollider { get; private set; }
    private Transform sign;
    private TMP_Text label;
    private Renderer face;
    private MaterialPropertyBlock properties;
    public void Build(Material material,string title,Color color)
    {
        sign=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;sign.name="Training sign";sign.SetParent(transform,false);
        sign.localPosition=new Vector3(0,.6f,0);sign.localScale=new Vector3(.75f,.32f,.04f);
        SelectionCollider=sign.GetComponent<Collider>();face=sign.GetComponent<Renderer>();face.sharedMaterial=material;
        properties=new MaterialPropertyBlock();SetColor(color);
        var text=new GameObject("Label").AddComponent<TextMeshPro>();text.transform.SetParent(sign,false);
        text.transform.localPosition=new Vector3(0,0,-.56f);text.transform.localScale=new Vector3(1/.75f,1/.32f,1/.04f);
        text.font=TMP_Settings.defaultFontAsset;
        text.rectTransform.sizeDelta=new Vector2(.7f,.27f);text.fontSize=1.1f;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.text=FormatTitle(title);text.raycastTarget=false;label=text;
        text.ForceMeshUpdate(true,true);
        var foot=GameObject.CreatePrimitive(PrimitiveType.Cylinder);foot.name="Ground marker";foot.transform.SetParent(transform,false);foot.transform.localScale=new Vector3(.3f,.012f,.3f);foot.transform.localPosition=new Vector3(0,.012f,0);Destroy(foot.GetComponent<Collider>());foot.GetComponent<Renderer>().sharedMaterial=material;foot.GetComponent<Renderer>().SetPropertyBlock(properties);
    }
    private static string FormatTitle(string value)=>value.Replace("TRAINING EXIT","TRAINING\nEXIT").Replace("ASSEMBLY POINT","ASSEMBLY\nPOINT").Replace("WAYPOINT ","WAYPOINT\n");
    public void SetTitle(string value){if(label!=null){label.text=FormatTitle(value);label.ForceMeshUpdate(true,true);}}
    public void SetColor(Color color){if(face==null)return;properties.SetColor("_BaseColor",color);face.SetPropertyBlock(properties);}
    public void Face(Camera camera,Vector3 normal)
    {
        if(camera==null||sign==null)return;
        Vector3 away=Vector3.ProjectOnPlane(sign.position-camera.transform.position,normal);
        if(away.sqrMagnitude>.001f)sign.rotation=Quaternion.LookRotation(away,normal);
    }
}
