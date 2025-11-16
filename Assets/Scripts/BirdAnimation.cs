using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdAnimation : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab shown when the bird is idle (not moving)")]
    public GameObject birdStandPrefab;
    [Tooltip("Prefabs for flying animation (will cycle through these)")]
    public List<GameObject> birdFlyPrefabs;

    [Header("Animation Settings")]
    [Tooltip("Speed of the flying animation (seconds per frame)")]
    public float animationSpeed = 0.2f;
    [Tooltip("Velocity threshold to determine if bird is moving")]
    public float movementThreshold = 0.2f;

    private Rigidbody2D _rigidbody2D;
    private float _animationTimer;
    private int _currentFlyIndex = 0;

    private GameObject _currentPrefabInstance;
    private GameObject _standPrefabInstance;
    private List<GameObject> _flyPrefabInstances;

    void Start()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();

        if (_rigidbody2D == null)
        {
            Debug.LogError("Rigidbody2D component not found on " + gameObject.name);
        }

        // Bird 오브젝트 자체의 스케일 확인
        Debug.Log($"Bird initial scale: {transform.localScale}");
        
        // 만약 스케일이 비정상적으로 작다면 경고
        if (transform.localScale.x < 0.1f || transform.localScale.y < 0.1f)
        {
            Debug.LogWarning($"Bird scale is very small ({transform.localScale}). Consider adjusting it in the Inspector.");
        }

        InitializePrefabs();
    }

    void InitializePrefabs()
    {
        // 스프라이트 렌더러가 있으면 비활성화 (프리팹이 대신 표시됨)
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // Bird 본체에 Collider2D가 있는지 확인 (클릭 감지를 위해 필요)
        Collider2D birdCollider = GetComponent<Collider2D>();
        if (birdCollider == null)
        {
            Debug.LogWarning("Bird has no Collider2D! Adding CircleCollider2D for click detection.");
            
            // 프리팹 크기를 기반으로 Collider 크기 계산
            float radius = 0.5f; // 기본값
            if (birdStandPrefab != null)
            {
                SpriteRenderer prefabRenderer = birdStandPrefab.GetComponentInChildren<SpriteRenderer>();
                if (prefabRenderer != null && prefabRenderer.sprite != null)
                {
                    // 스프라이트의 크기를 기반으로 radius 계산
                    Bounds bounds = prefabRenderer.sprite.bounds;
                    radius = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f;
                    Debug.Log($"Auto-calculated collider radius: {radius} based on prefab sprite");
                }
            }
            
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.radius = radius;
        }

        _flyPrefabInstances = new List<GameObject>();

        // Stand 프리팹 인스턴스 생성
        if (birdStandPrefab != null)
        {
            _standPrefabInstance = Instantiate(birdStandPrefab, transform);
            _standPrefabInstance.transform.localPosition = Vector3.zero;
            _standPrefabInstance.transform.localScale = Vector3.one; // 스케일 리셋
            CleanupPrefabPhysics(_standPrefabInstance);
            _standPrefabInstance.SetActive(true);
            _currentPrefabInstance = _standPrefabInstance;
        }
        else
        {
            Debug.LogWarning("Bird Stand Prefab is not assigned!");
        }

        // Fly 프리팹 인스턴스들 생성 (비활성 상태로)
        if (birdFlyPrefabs != null && birdFlyPrefabs.Count > 0)
        {
            foreach (var prefab in birdFlyPrefabs)
            {
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab, transform);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localScale = Vector3.one; // 스케일 리셋
                    CleanupPrefabPhysics(instance);
                    instance.SetActive(false);
                    _flyPrefabInstances.Add(instance);
                }
            }
        }
        else
        {
            Debug.LogWarning("Bird Fly Prefabs are not assigned!");
        }
    }

    void CleanupPrefabPhysics(GameObject prefabInstance)
    {
        // 프리팹과 모든 자식들의 물리 컴포넌트 즉시 제거
        // (부모 오브젝트의 물리 시스템을 방해하지 않도록)
        
        // Rigidbody2D 제거
        Rigidbody2D[] rigidbodies = prefabInstance.GetComponentsInChildren<Rigidbody2D>();
        foreach (var rb in rigidbodies)
        {
            DestroyImmediate(rb);
        }

        // Collider2D 제거
        Collider2D[] colliders = prefabInstance.GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            DestroyImmediate(col);
        }
    }

    void Update()
    {
        UpdateAnimation();
    }

    void UpdateAnimation()
    {
        if (_rigidbody2D == null) return;

        // 새가 움직이고 있는지 확인
        bool isMoving = !_rigidbody2D.isKinematic && 
                        _rigidbody2D.velocity.magnitude > movementThreshold;

        if (isMoving)
        {
            // 움직이는 중: birdFlyPrefabs 리스트의 프리팹들을 순환
            if (_flyPrefabInstances != null && _flyPrefabInstances.Count > 0)
            {
                _animationTimer += Time.deltaTime;
                if (_animationTimer >= animationSpeed)
                {
                    _animationTimer = 0f;
                    
                    // 현재 활성화된 프리팹 비활성화
                    if (_currentPrefabInstance != null)
                    {
                        _currentPrefabInstance.SetActive(false);
                    }
                    
                    // 다음 프리팹 활성화
                    _currentFlyIndex = (_currentFlyIndex + 1) % _flyPrefabInstances.Count;
                    _currentPrefabInstance = _flyPrefabInstances[_currentFlyIndex];
                    _currentPrefabInstance.SetActive(true);
                }
            }
        }
        else
        {
            // 멈춰 있음: birdStandPrefab 표시
            if (_standPrefabInstance != null)
            {
                // 현재 활성화된 프리팹 비활성화
                if (_currentPrefabInstance != null && _currentPrefabInstance != _standPrefabInstance)
                {
                    _currentPrefabInstance.SetActive(false);
                }
                
                // Stand 프리팹 활성화
                _standPrefabInstance.SetActive(true);
                _currentPrefabInstance = _standPrefabInstance;
            }
            _animationTimer = 0f;
            _currentFlyIndex = 0;
        }
    }

    void OnDestroy()
    {
        // 프리팹 인스턴스들 정리
        if (_standPrefabInstance != null)
        {
            Destroy(_standPrefabInstance);
        }

        if (_flyPrefabInstances != null)
        {
            foreach (var instance in _flyPrefabInstances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }
        }
    }
}
