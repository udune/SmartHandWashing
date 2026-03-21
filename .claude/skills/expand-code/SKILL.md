---
name: expand-code
description: Expand compact code into readable beginner-friendly style without changing logic
argument-hint: [file-path]
allowed-tools: Read, Edit, Glob
---

# Expand Code Style

파일 `$ARGUMENTS`의 코드를 **로직 변경 없이** 초보자가 읽기 쉬운 정석 스타일로 확장합니다.

## 변환 규칙

### 1. 중괄호 항상 사용
```csharp
// Before
if (condition) DoSomething();

// After
if (condition)
{
    DoSomething();
}
```

### 2. 한 줄에 하나의 statement
```csharp
// Before
int a = 1; int b = 2; int c = 3;

// After
int a = 1;
int b = 2;
int c = 3;
```

### 3. 복잡한 삼항 연산자 → if-else
```csharp
// Before
var result = condition ? valueA : condition2 ? valueB : valueC;

// After
string result;
if (condition)
{
    result = valueA;
}
else if (condition2)
{
    result = valueB;
}
else
{
    result = valueC;
}
```

### 4. 메서드 체이닝 줄바꿈
```csharp
// Before
items.Where(x => x.IsActive).Select(x => x.Name).ToList();

// After
items
    .Where(x => x.IsActive)
    .Select(x => x.Name)
    .ToList();
```

### 5. 표현식 본문 멤버 → 블록 본문
```csharp
// Before
public int Count => _items.Count;
public void Log() => Debug.Log("msg");

// After
public int Count
{
    get { return _items.Count; }
}

public void Log()
{
    Debug.Log("msg");
}
```

### 6. 복합 조건 줄바꿈
```csharp
// Before
if (a && b && c || d && e)

// After
if ((a && b && c)
    || (d && e))
```

### 7. 유지할 것들
- `?.` `??` null 조건부 연산자 (가독성 좋음)
- 간단한 람다 `x => x.Id`
- 주석 그대로 유지

## 지시사항

1. 파일을 읽습니다
2. 위 규칙에 따라 포맷팅만 변경합니다
3. **로직, 변수명, 알고리즘 절대 변경 금지**
4. 변경된 코드를 파일에 적용합니다

## 금지 사항

- 새로운 기능 추가 금지
- 버그 수정 금지
- 변수명/메서드명 변경 금지
- 오직 **포맷팅과 정렬**만 수행
