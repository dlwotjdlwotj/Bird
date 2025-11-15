using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdMovement : MonoBehaviour
{
    [Header("References")]
    public GameObject floor;
    public List<GameObject> obstacles;
    public GameObject goal;

    [Header("Launch Settings")]
    [Tooltip("Maximum distance the bird can be pulled back from the start position.")]
    public float maxDragDistance = 3f;
    [Tooltip("Multiplier applied to the release direction to determine launch speed.")]
    public float launchPower = 5f;

    [Header("Visuals")]
    [Tooltip("Optional LineRenderer used to show the launch direction and power.")]
    public LineRenderer dragArrow;
    [Tooltip("Multiplier applied to the arrow length relative to the drag distance.")]
    public float arrowScale = 1f;

    Vector2 _startPosition, _initialPosition;
    Rigidbody2D _birdRigidbody;
    bool _isDragging;
    Vector2 _currentDragPosition;
    bool _hasLaunched;

    void Awake()
    {
        _birdRigidbody = GetComponent<Rigidbody2D>();
        if (_birdRigidbody == null)
        {
            _birdRigidbody = gameObject.AddComponent<Rigidbody2D>();
        }
    }

    void Start()
    {
        _startPosition = transform.position;
        _initialPosition = transform.position;
        _currentDragPosition = _startPosition;
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        if (dragArrow != null)
        {
            dragArrow.positionCount = 2;
            dragArrow.useWorldSpace = true;
            dragArrow.enabled = false;
        }
    }

    void Update()
    {
        HandleInput();
        UpdateArrow();

        // floor 아래로 떨어졌는지 확인
        if (floor != null && transform.position.y < floor.transform.position.y - 2.5f)
        {
            HandleFailure();
        }
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!_hasLaunched && IsPointerOverBird())
            {
                _isDragging = true;
            }
        }
        else if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            ReleaseBird();
        }
    }

    void UpdateDragVector()
    {
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pointerWorldPos.z = 0f;
        Vector2 desiredPosition = pointerWorldPos;

        Vector2 directionFromStart = desiredPosition - _startPosition;
        if (directionFromStart.magnitude > maxDragDistance)
        {
            directionFromStart = directionFromStart.normalized * maxDragDistance;
        }

        _currentDragPosition = _startPosition + directionFromStart;
    }

    void ReleaseBird()
    {
        _isDragging = false;
        _hasLaunched = true;
        SetBirdPhysics(isKinematic: false, resetVelocity: true);

        Vector2 releaseDirection = _startPosition - _currentDragPosition;
        _birdRigidbody.velocity = releaseDirection * launchPower;

        _currentDragPosition = _startPosition;
        HideArrow();
    }

    bool IsPointerOverBird()
    {
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pointerWorldPos.z = 0f;
        Collider2D hit = Physics2D.OverlapPoint(pointerWorldPos);
        return hit != null && hit.gameObject == gameObject;
    }

    void SetBirdPhysics(bool isKinematic, bool resetVelocity)
    {
        _birdRigidbody.isKinematic = isKinematic;
        if (resetVelocity)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }
    }

    void UpdateArrow()
    {
        if (dragArrow == null)
        {
            return;
        }

        if (_isDragging)
        {
            UpdateDragVector();
        }

        if (_isDragging)
        {
            Vector2 releaseDirection = _startPosition - _currentDragPosition;
            if (releaseDirection.sqrMagnitude > Mathf.Epsilon)
            {
                dragArrow.enabled = true;
                Vector3 start = _startPosition;
                Vector3 end = _startPosition + releaseDirection.normalized * releaseDirection.magnitude * arrowScale;
                dragArrow.SetPosition(0, start);
                dragArrow.SetPosition(1, end);
            }
            else
            {
                dragArrow.enabled = false;
            }
        }
        else
        {
            HideArrow();
        }
    }

    void HideArrow()
    {
        if (dragArrow != null)
        {
            dragArrow.enabled = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject other = collision.gameObject;
        Debug.Log($"[COLLISION] {gameObject.name} collided with {other.name}");

        // goal와 충돌 - 성공
        if (goal != null && other == goal)
        {
            Debug.Log("BirdMovement: Hit goal - SUCCESS!");
            HandleSuccess();
            return;
        }

        // Obstacle과 충돌 - 실패 (재시작)
        if (obstacles != null && obstacles.Contains(other))
        {
            Debug.Log("BirdMovement: Hit obstacle - FAILURE!");
            HandleFailure();
            return;
        }

        // obstacle이나 goal이 아닌 다른 물체와 충돌 - 멈춤
        Debug.Log($"BirdMovement: Hit other object ({other.name}) - Stopping bird");
        StopBirdAndAllowReload(collision);
    }

    void HandleSuccess()
    {
        Debug.Log("Bird reached the goal: Success!");
        ResetBirdState();
    }

    void HandleFailure()
    {
        Debug.Log("Bird hit an obstacle or fell: Restarting.");
        ResetBirdState();
    }

    void StopBirdAndAllowReload(Collision2D collision)
    {
        // 새를 멈춤
        if (_birdRigidbody != null)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }

        // 충돌 방향 계산
        Vector2 collisionNormal = Vector2.zero;
        Vector2 contactPoint = Vector2.zero;
        if (collision.contacts.Length > 0)
        {
            collisionNormal = collision.contacts[0].normal;
            contactPoint = collision.contacts[0].point;
        }

        // 새의 Collider 크기 계산
        Collider2D birdCollider = GetComponent<Collider2D>();
        float colliderRadius = 0.5f; // 기본값
        if (birdCollider != null)
        {
            if (birdCollider is CircleCollider2D)
            {
                colliderRadius = ((CircleCollider2D)birdCollider).radius;
            }
            else if (birdCollider is BoxCollider2D)
            {
                colliderRadius = Mathf.Max(((BoxCollider2D)birdCollider).size.x, ((BoxCollider2D)birdCollider).size.y) * 0.5f;
            }
        }

        // 안전한 거리 (Collider 반지름)
        float safeDistance = colliderRadius;
        
        // 충돌한 물체 방향으로 Raycast를 쏴서 안전한 위치 찾기
        Vector2 newPosition = FindSafePosition(contactPoint, collisionNormal, safeDistance, collision.gameObject);

        transform.position = newPosition;

        // 현재 위치를 새로운 시작 위치로 설정
        _isDragging = false;
        _hasLaunched = false;
        _startPosition = newPosition;
        _currentDragPosition = _startPosition;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped. Ready to launch again from current position.");
    }

    Vector2 FindSafePosition(Vector2 contactPoint, Vector2 normal, float minDistance, GameObject collisionObject)
    {
        // 새의 Collider를 일시적으로 비활성화
        Collider2D birdCollider = GetComponent<Collider2D>();
        bool wasEnabled = birdCollider != null && birdCollider.enabled;
        if (birdCollider != null)
        {
            birdCollider.enabled = false;
        }

        // normal 방향(벽에서 멀어지는 방향)으로 안전한 거리 찾기
        float checkDistance = minDistance;
        Vector2 testPosition = contactPoint + normal * checkDistance;
        
        // 최대 20번 반복하여 안전한 위치 찾기
        for (int i = 0; i < 20; i++)
        {
            // testPosition에서 OverlapCircle을 사용하여 충돌 체크
            Collider2D overlap = Physics2D.OverlapCircle(testPosition, minDistance * 0.8f);
            
            // 충돌한 물체가 없거나, 충돌한 물체가 원래 충돌한 물체가 아니면 안전한 위치
            if (overlap == null || overlap.gameObject != collisionObject)
            {
                // 추가 확인: 벽 방향으로 Raycast를 쏴서 충분한 거리가 있는지 확인
                RaycastHit2D hit = Physics2D.Raycast(testPosition, -normal, minDistance);
                if (hit.collider == null || hit.collider.gameObject != collisionObject)
                {
                    // 안전한 위치 찾음
                    break;
                }
            }
            
            // 충돌했으면 더 멀리 이동
            checkDistance += 0.1f;
            testPosition = contactPoint + normal * checkDistance;
        }

        // Collider 복원
        if (birdCollider != null)
        {
            birdCollider.enabled = wasEnabled;
        }

        return testPosition;
    }

    void ResetBirdState()
    {
        _startPosition = _initialPosition;
        
        _isDragging = false;
        _hasLaunched = false;
        _currentDragPosition = _startPosition;

        transform.position = _startPosition;
        transform.rotation = Quaternion.identity;

        SetBirdPhysics(isKinematic: true, resetVelocity: true);
        HideArrow();
    }
}
