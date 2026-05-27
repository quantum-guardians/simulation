using UnityEngine;
using UnityEngine.Rendering;

public sealed class PedestrianViewFactory
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Material _material;
    MaterialPropertyBlock _mpb;
    Transform _root;
    Color _baseColor;
    Color _selectedTint;

    public Transform Root => _root;

    public void Configure(Transform owner, Transform root, Color baseColor, Color selectedTint)
    {
        _root = root;
        _baseColor = baseColor;
        _selectedTint = selectedTint;
        EnsureRoot(owner);
        EnsureMaterial();
        _mpb ??= new MaterialPropertyBlock();
    }

    public PedestrianAgentRuntime Create(
        int id,
        float radius,
        float height,
        Vector3 position,
        Vector3 forward,
        PedestrianCrowdSim owner)
    {
        EnsureMaterial();
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Pedestrian_{id}";
        go.transform.SetParent(_root, false);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.transform.position = position;
        FaceAlong(go.transform, forward);

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = _material;
            rend.shadowCastingMode = ShadowCastingMode.Off;
        }

        var unit = go.AddComponent<PedestrianUnit>();
        unit.Initialize(owner, id);

        return new PedestrianAgentRuntime
        {
            Id = id,
            Transform = go.transform,
            Renderer = rend
        };
    }

    public void ApplyHighlight(PedestrianAgentRuntime agent, bool selected)
    {
        if (agent.Renderer == null) return;
        var c = selected ? Color.Lerp(_baseColor, _selectedTint, 0.55f) : _baseColor;
        _mpb.Clear();
        if (agent.Renderer.sharedMaterial != null && agent.Renderer.sharedMaterial.HasProperty(BaseColorId))
            _mpb.SetColor(BaseColorId, c);
        else if (agent.Renderer.sharedMaterial != null && agent.Renderer.sharedMaterial.HasProperty(ColorId))
            _mpb.SetColor(ColorId, c);
        agent.Renderer.SetPropertyBlock(_mpb);
    }

    public void DestroyAgent(PedestrianAgentRuntime agent)
    {
        if (agent.Transform != null)
            Object.Destroy(agent.Transform.gameObject);
    }

    public void Dispose()
    {
        if (_material != null)
            Object.Destroy(_material);
    }

    void EnsureRoot(Transform owner)
    {
        if (_root != null) return;
        var go = new GameObject("Pedestrians");
        go.transform.SetParent(owner, false);
        _root = go.transform;
    }

    void EnsureMaterial()
    {
        if (_material != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Unlit/Color");
        if (shader == null) return;
        _material = new Material(shader);
        ApplyBaseColor(_material, _baseColor);
    }

    static void ApplyBaseColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId))
            m.SetColor(BaseColorId, c);
        else if (m.HasProperty(ColorId))
            m.SetColor(ColorId, c);
        else
            m.color = c;
    }

    public static void FaceAlong(Transform tr, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude > 1e-8f)
            tr.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
