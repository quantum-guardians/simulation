using UnityEngine;

public sealed class NodeLabelBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var toCam = transform.position - cam.transform.position;
        if (toCam.sqrMagnitude < 1e-8f) return;
        transform.rotation = Quaternion.LookRotation(toCam);
    }
}
