using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 战斗特效工厂 — 纯代码生成特效 GameObject（不依赖外部资源）
/// 使用 Canvas(WorldSpace) + Image + DOTween 动画，不用 ParticleSystem
/// 特效播放完毕自动回收到 BattleEffectManager 的对象池
/// </summary>
public class BattleEffectFactory : IBattleEffectProvider
{
    // ── 特效类型常量 ──
    public const string EFFECT_HIT      = "Hit";
    public const string EFFECT_HEAL     = "Heal";
    public const string EFFECT_SHIELD   = "Shield";
    public const string EFFECT_CRIT     = "Crit";
    public const string EFFECT_DEATH    = "Death";
    public const string EFFECT_LEVELUP  = "LevelUp";

    /// <summary>
    /// 播放指定类型的特效
    /// </summary>
    /// <param name="pos">世界坐标位置</param>
    /// <param name="effectType">特效类型标识</param>
    public void PlayEffect(Vector3 pos, string effectType)
    {
        switch (effectType)
        {
            case EFFECT_HIT:     PlayHitEffect(pos);     break;
            case EFFECT_HEAL:    PlayHealEffect(pos);    break;
            case EFFECT_SHIELD:  PlayShieldEffect(pos);  break;
            case EFFECT_CRIT:    PlayCritEffect(pos);    break;
            case EFFECT_DEATH:   PlayDeathEffect(pos);   break;
            case EFFECT_LEVELUP: PlayLevelUpEffect(pos); break;
        }
    }

    // ================================================================
    // Hit: 红色缩放圆圈（Scale 0→1.5→0，0.3s）
    // ================================================================

    private void PlayHitEffect(Vector3 pos)
    {
        var go = CreateEffectBase(pos, "HitEffect", 80);
        var img = go.GetComponent<Image>();
        var rt = go.GetComponent<RectTransform>();
        if (img == null || rt == null) { SafeRecycle(EFFECT_HIT, go); return; }

        img.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        rt.sizeDelta = new Vector2(80, 80);

        var seq = DOTween.Sequence();
        seq.Append(rt.DOScale(1.5f, 0.15f).SetEase(Ease.OutQuad));
        seq.Append(rt.DOScale(0f, 0.15f).SetEase(Ease.InQuad));
        seq.Join(img.DOFade(0f, 0.15f));
        seq.OnComplete(() => Recycle(EFFECT_HIT, go));
    }

    // ================================================================
    // Heal: 绿色上飘"+"号（3个粒子依次飘起）
    // ================================================================

    private void PlayHealEffect(Vector3 pos)
    {
        for (int i = 0; i < 3; i++)
        {
            float delay = i * 0.15f;
            var go = CreateEffectBase(pos, "HealEffect", 80);
            var img = go.GetComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            var canvas = go.GetComponent<Canvas>();
            if (img == null || rt == null) { SafeRecycle(EFFECT_HEAL, go); continue; }

            img.color = new Color(0.2f, 1f, 0.3f, 1f);
            rt.sizeDelta = new Vector2(50, 50);

            if (canvas != null) canvas.sortingOrder = 10 + i;

            var seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(rt.DOLocalMoveY(rt.localPosition.y + 60f, 0.6f).SetEase(Ease.OutQuad));
            seq.Join(img.DOFade(0f, 0.6f));
            seq.Join(rt.DOScale(1.2f, 0.6f));
            seq.OnComplete(() => Recycle(EFFECT_HEAL, go));
        }
    }

    // ================================================================
    // Shield: 蓝色六边形轮廓（Outline效果，0.5s渐隐）
    // ================================================================

    private void PlayShieldEffect(Vector3 pos)
    {
        var go = CreateEffectBase(pos, "ShieldEffect", 80);
        var img = go.GetComponent<Image>();
        var rt = go.GetComponent<RectTransform>();
        if (img == null || rt == null) { SafeRecycle(EFFECT_SHIELD, go); return; }

        img.color = new Color(0.2f, 0.5f, 1f, 0.8f);
        rt.sizeDelta = new Vector2(90, 90);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 1f, 1f);
        outline.effectDistance = new Vector2(4, 4);

        var seq = DOTween.Sequence();
        seq.Append(rt.DOScale(1.2f, 0.25f).SetEase(Ease.OutQuad));
        seq.Append(img.DOFade(0f, 0.25f));
        seq.OnComplete(() => Recycle(EFFECT_SHIELD, go));
    }

    // ================================================================
    // Crit: 金色爆炸（8个方向射线扩散 + 震屏）
    // ================================================================

    private void PlayCritEffect(Vector3 pos)
    {
        // 震屏效果
        BattleEffectManager.Instance?.CameraShake(0.3f, 0.4f);

        // 中心金色闪光
        var centerGo = CreateEffectBase(pos, "CritCenter", 80);
        var centerImg = centerGo.GetComponent<Image>();
        var centerRt = centerGo.GetComponent<RectTransform>();
        if (centerImg != null && centerRt != null)
        {
            centerImg.color = new Color(1f, 0.85f, 0.1f, 1f);
            centerRt.sizeDelta = new Vector2(100, 100);

            var centerSeq = DOTween.Sequence();
            centerSeq.Append(centerRt.DOScale(2f, 0.15f).SetEase(Ease.OutQuad));
            centerSeq.Append(centerRt.DOScale(0f, 0.15f).SetEase(Ease.InQuad));
            centerSeq.Join(centerImg.DOFade(0f, 0.15f));
            centerSeq.OnComplete(() => Recycle(EFFECT_CRIT, centerGo));
        }
        else
        {
            SafeRecycle(EFFECT_CRIT, centerGo);
        }

        // 8个方向的射线
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            float rad = angle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);

            var rayGo = CreateEffectBase(pos, "CritRay", 80);
            var rayImg = rayGo.GetComponent<Image>();
            var rayRt = rayGo.GetComponent<RectTransform>();
            if (rayImg == null || rayRt == null) { SafeRecycle(EFFECT_CRIT, rayGo); continue; }

            rayImg.color = new Color(1f, 0.75f, 0f, 0.9f);
            rayRt.sizeDelta = new Vector2(8, 30);
            rayRt.localEulerAngles = new Vector3(0, 0, angle - 90f);

            var seq = DOTween.Sequence();
            seq.Append(rayRt.DOLocalMove(rayRt.localPosition + dir * 80f, 0.3f).SetEase(Ease.OutQuad));
            seq.Join(rayImg.DOFade(0f, 0.3f));
            seq.OnComplete(() => Recycle(EFFECT_CRIT, rayGo));
        }
    }

    // ================================================================
    // Death: 灰色碎裂（缩小+旋转+透明度渐隐）
    // ================================================================

    private void PlayDeathEffect(Vector3 pos)
    {
        for (int i = 0; i < 6; i++)
        {
            var go = CreateEffectBase(pos, "DeathFragment", 80);
            var img = go.GetComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            if (img == null || rt == null) { SafeRecycle(EFFECT_DEATH, go); continue; }

            img.color = new Color(0.5f, 0.5f, 0.5f, 0.9f);
            rt.sizeDelta = new Vector2(20, 20);

            float randomAngle = Random.Range(0f, 360f);
            float randomDist = Random.Range(30f, 80f);
            var targetPos = rt.localPosition + new Vector3(
                Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomDist,
                Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomDist, 0);

            var seq = DOTween.Sequence();
            seq.Append(rt.DOLocalMove(targetPos, 0.5f).SetEase(Ease.OutQuad));
            seq.Join(rt.DOScale(0.2f, 0.5f).SetEase(Ease.InQuad));
            seq.Join(rt.DOLocalRotate(new Vector3(0, 0, Random.Range(-180f, 180f)), 0.5f));
            seq.Join(img.DOFade(0f, 0.5f));
            seq.OnComplete(() => Recycle(EFFECT_DEATH, go));
        }
    }

    // ================================================================
    // LevelUp: 金色光柱（竖直矩形从下到上，0.6s）
    // ================================================================

    private void PlayLevelUpEffect(Vector3 pos)
    {
        var go = CreateEffectBase(pos, "LevelUpBeam", 80);
        var img = go.GetComponent<Image>();
        var rt = go.GetComponent<RectTransform>();
        if (img == null || rt == null) { SafeRecycle(EFFECT_LEVELUP, go); return; }

        img.color = new Color(1f, 0.85f, 0.1f, 0.8f);
        rt.sizeDelta = new Vector2(30, 0);

        var seq = DOTween.Sequence();
        seq.Append(rt.DOSizeDelta(new Vector2(30, 200), 0.3f).SetEase(Ease.OutQuad));
        seq.Join(rt.DOLocalMoveY(rt.localPosition.y + 50f, 0.3f).SetEase(Ease.OutQuad));
        seq.Append(img.DOFade(0f, 0.3f));
        seq.Join(rt.DOSizeDelta(new Vector2(50, 200), 0.3f));
        seq.OnComplete(() => Recycle(EFFECT_LEVELUP, go));
    }

    // ================================================================
    // 工具方法
    // ================================================================

    /// <summary>
    /// 创建特效基础 GameObject — 优先从对象池获取，池空时才创建新对象
    /// </summary>
    private GameObject CreateEffectBase(Vector3 worldPos, string name, int sortingOrder)
    {
        GameObject go;

        // 优先从对象池获取
        if (BattleEffectManager.Instance != null)
        {
            go = BattleEffectManager.Instance.GetFromPool(name);
            if (go != null)
            {
                // 复用已有对象，重置状态
                go.name = name;
                go.transform.position = worldPos;
                go.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                go.transform.localRotation = Quaternion.identity;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(100, 100);
                    rt.localPosition = worldPos;
                }

                var canvas = go.GetComponent<Canvas>();
                if (canvas != null) canvas.sortingOrder = sortingOrder;

                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    img.color = Color.white;
                    img.raycastTarget = false;
                }

                return go;
            }
        }

        // 池空，创建新对象
        go = new GameObject(name);
        go.transform.position = worldPos;

        // WorldSpace Canvas
        var newCanvas = go.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.WorldSpace;
        newCanvas.sortingOrder = sortingOrder;
        newCanvas.overrideSorting = true;

        // CanvasRenderer + Image
        go.AddComponent<CanvasRenderer>();
        var newImg = go.AddComponent<Image>();
        newImg.raycastTarget = false;

        // 设置 Canvas 大小和缩放
        var canvasRt = go.GetComponent<RectTransform>();
        if (canvasRt != null)
        {
            canvasRt.sizeDelta = new Vector2(100, 100);
            canvasRt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }

        return go;
    }

    /// <summary>
    /// 回收特效到对象池
    /// </summary>
    private void Recycle(string effectType, GameObject go)
    {
        if (go == null) return;

        // 重置状态
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        BattleEffectManager.Instance?.ReturnToPool(effectType, go);
    }

    /// <summary>
    /// 安全回收 — 组件缺失时直接 Destroy 而非回收到池
    /// </summary>
    private void SafeRecycle(string effectType, GameObject go)
    {
        if (go == null) return;

        // 组件缺失的对象不应回池，直接销毁
        Debug.LogWarning($"[BattleEffectFactory] 特效对象缺少组件，销毁而非回池: {go.name}");
        Object.Destroy(go);
    }
}
