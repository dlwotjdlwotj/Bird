using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdMovement : MonoBehaviour
{
    [Header("References")]
    public List<GameObject> obstacles;
    public GameObject goal;

    public float safeDistance = 0.3f;

    [Header("Launch Settings")]
    [Tooltip("Maximum distance the bird can be pulled back from the start position.")]
    public float maxDragDistance = 3f;
    [Tooltip("Multiplier applied to the release direction to determine launch speed.")]
    public float launchPower = 5f;

    [Header("Visuals")]
    [Tooltip("Prefab used to show the launch direction and power.")]
    public GameObject dragArrowPrefab;
    [Tooltip("Multiplier applied to the arrow length relative to the drag distance.")]
    public float arrowScale = 0.3f;

    Vector2 _startPosition, _initialPosition;
    Rigidbody2D _birdRigidbody;
    bool _isDragging;
    Vector2 _currentDragPosition;
    bool _hasLaunched;
    bool _isOnFloor;
    bool _isStopped;
    float _stopVelocityThreshold = 0.2f; // 속도가 이 값 이하로 떨어지면 멈춤
    
    GameObject _dragArrowInstance;

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

        // Arrow prefab 인스턴스 생성
        if (dragArrowPrefab != null)
        {
            _dragArrowInstance = Instantiate(dragArrowPrefab);
            _dragArrowInstance.SetActive(false);
        }
    }

    void Update()
    {
        HandleInput();
        UpdateArrow();

        // floor 아래로 떨어졌는지 확인 (첫 번째 floor 기준, 태그로 체크)
        GameObject firstFloor = GameObject.FindGameObjectWithTag("Floor");
        if (firstFloor != null && transform.position.y < firstFloor.transform.position.y - 2.5f)
        {
            HandleFailure();
        }

        // floor에 닿아서 굴러가는 중이면 속도 체크
        if (_isOnFloor && _hasLaunched && _birdRigidbody != null && !_birdRigidbody.isKinematic)
        {
            float velocityMagnitude = _birdRigidbody.velocity.magnitude;
            if (velocityMagnitude < _stopVelocityThreshold)
            {
                // 속도가 충분히 느려지면 멈춤
                StopBirdOnFloor();
            }
        }
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 장애물에 부딪혀서 멈춘 상태에서 터치하면 리스타트
            if (_isStopped)
            {
                ResetBirdState();
                return;
            }

            // 멈춘 상태가 아니고 아직 발사하지 않았으면 드래그 시작
            if (!_hasLaunched && IsPointerOverBird())
            {
                _isDragging = true;
            }
        }
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            // 드래그 중일 때 위치 업데이트
            UpdateDragVector();
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
        
        if (hit == null) return false;
        
        // Bird 자체를 클릭했거나, Bird의 자식을 클릭한 경우 true
        return hit.gameObject == gameObject || hit.transform.IsChildOf(transform);
    }

    void SetBirdPhysics(bool isKinematic, bool resetVelocity)
    {
        _birdRigidbody.isKinematic = isKinematic;
        // kinematic이 아닐 때만 중력 적용
        if (!isKinematic)
        {
            _birdRigidbody.gravityScale = 1f;
        }
        else
        {
            _birdRigidbody.gravityScale = 0f; // kinematic일 때는 중력 없음
        }
        if (resetVelocity)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }
    }

    void UpdateArrow()
    {
        if (_dragArrowInstance == null)
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
                _dragArrowInstance.SetActive(true);
                
                // 화살표 크기를 드래그 거리에 비례하여 조정 (Y축으로 늘어남)
                float distance = releaseDirection.magnitude * arrowScale;
                _dragArrowInstance.transform.localScale = new Vector3(1f, distance, 1f);
                
                // 화살표가 발사 방향을 가리키도록 회전 (위쪽이 기본 방향이므로 -90도 보정)
                float angle = Mathf.Atan2(releaseDirection.y, releaseDirection.x) * Mathf.Rad2Deg - 90f;
                _dragArrowInstance.transform.rotation = Quaternion.Euler(0, 0, angle);
                
                // 화살표 바닥이 새 위치에 오도록 설정 (화살표 중심을 위로 이동)
                Vector2 arrowOffset = releaseDirection.normalized * (distance * 0.5f);
                _dragArrowInstance.transform.position = new Vector3(
                    _startPosition.x + arrowOffset.x,
                    _startPosition.y + arrowOffset.y,
                    0f
                );
            }
            else
            {
                _dragArrowInstance.SetActive(false);
            }
        }
        else
        {
            HideArrow();
        }
    }

    void HideArrow()
    {
        if (_dragArrowInstance != null)
        {
            _dragArrowInstance.SetActive(false);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject other = collision.gameObject;
        Debug.Log($"[COLLISION] {gameObject.name} collided with {other.name}");

        // 자식 오브젝트와의 충돌은 무시 (애니메이션 프리팹)
        if (other.transform.IsChildOf(transform))
        {
            Debug.Log("BirdMovement: Collision with child object - Ignoring");
            return;
        }

        // goal와 충돌 - 성공
        if (goal != null && other == goal)
        {
            Debug.Log("BirdMovement: Hit goal - SUCCESS!");
            HandleSuccess();
            return;
        }

        // Obstacle 태그를 가진 오브젝트 또는 그 부모와 충돌 - 즉시 멈춤
        if (other.CompareTag("Obstacle") || (other.transform.parent != null && other.transform.parent.CompareTag("Obstacle")))
        {
            Debug.Log("BirdMovement: Hit obstacle - Stopping immediately!");
            StopBirdImmediately();
            return;
        }

        // Obstacle 리스트에 있는 오브젝트와 충돌 - 즉시 멈춤 (기존 호환성)
        if (obstacles != null)
        {
            // 충돌한 오브젝트 자체가 리스트에 있는지 확인
            if (obstacles.Contains(other))
            {
                Debug.Log($"BirdMovement: Hit obstacle in list ({other.name}) - Stopping immediately!");
                StopBirdImmediately();
                return;
            }
            
            // 충돌한 오브젝트의 부모가 리스트에 있는지 확인
            if (other.transform.parent != null && obstacles.Contains(other.transform.parent.gameObject))
            {
                Debug.Log($"BirdMovement: Hit child of obstacle in list ({other.name}) - Stopping immediately!");
                StopBirdImmediately();
                return;
            }
        }

        // Floor 태그를 가진 오브젝트 또는 그 부모와 충돌 - 굴러가기 시작
        if (other.CompareTag("Floor") || (other.transform.parent != null && other.transform.parent.CompareTag("Floor")))
        {
            Debug.Log("BirdMovement: Hit floor - Bird will roll");
            _isOnFloor = true;
            // 물리 시뮬레이션은 계속 유지 (굴러가도록)
            return;
        }

        // obstacle, goal, floor가 아닌 다른 물체와 충돌 - 무시 (계속 날아감)
        Debug.Log($"BirdMovement: Hit other object ({other.name}) - Ignoring");
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

    void StopBirdImmediately()
    {
        // 새를 즉시 멈춤
        if (_birdRigidbody != null)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }

        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isStopped = true;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped. Touch to restart.");
    }

    void StopBirdOnFloor()
    {
        if (_birdRigidbody == null) return;

        // 새를 멈춤
        _birdRigidbody.velocity = Vector2.zero;
        _birdRigidbody.angularVelocity = 0f;

        // 현재 위치를 새로운 시작 위치로 설정
        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isStopped = false; // floor에서 멈춘 경우는 리셋 안 함, 다시 발사 가능
        _startPosition = transform.position;
        _currentDragPosition = _startPosition;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped on floor. Can launch again from current position.");
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

        
        // 충돌한 물체 방향으로 Raycast를 쏴서 안전한 위치 찾기
        Vector2 newPosition = FindSafePosition(contactPoint, collisionNormal, safeDistance, collision.gameObject);

        transform.position = newPosition;

        // 현재 위치를 새로운 시작 위치로 설정
        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isStopped = true;
        _startPosition = newPosition;
        _currentDragPosition = _startPosition;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped. Touch to restart.");
    }

    Vector2 FindSafePosition(Vector2 contactPoint, Vector2 normal, float minDistance, GameObject collisionObject)
    {
        // 장애물 반대 방향(normal)으로 일정 거리 떨어진 위치에 배치
        // 충분한 안전 거리 확보 (collider 크기의 2배)
        float safeDistance = minDistance * 2f;
        Vector2 safePosition = contactPoint + normal * safeDistance;
        
        return safePosition;
    }

    void ResetBirdState()
    {
        _startPosition = _initialPosition;
        
        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isStopped = false;
        _currentDragPosition = _startPosition;

        transform.position = _startPosition;
        transform.rotation = Quaternion.identity;

        SetBirdPhysics(isKinematic: true, resetVelocity: true);
        HideArrow();

        Debug.Log("Bird reset to initial position..");
    }

    void OnDestroy()
    {
        // Arrow 인스턴스 정리
        if (_dragArrowInstance != null)
        {
            Destroy(_dragArrowInstance);
        }
    }
}
