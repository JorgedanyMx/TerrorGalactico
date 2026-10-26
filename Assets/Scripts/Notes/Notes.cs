
using UnityEngine;
using TMPro;

public class Notes : MonoBehaviour
{
    [Header("Textos")]
    public string Nname = "";
    public string NCont="";

    [Header("Referencias")]
    public TMP_Text NameNoteRef;
    public TMP_Text ContentNoteRef;

    private void Start()
    {
        NameNoteRef.text = Nname;
        ContentNoteRef.text = NCont;
    }
    public void SetNoteText(string sname,string sCont)
    {
        if (Nname !="")
        {
            NameNoteRef.text = sname;
            Nname = sname;
        }

        if (NCont != "")
        {
            ContentNoteRef.text = sCont;
            NCont = sCont;
        }
           
    }

}
