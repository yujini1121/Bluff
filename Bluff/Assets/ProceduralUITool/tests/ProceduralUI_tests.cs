using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using ProceduralUITool.Runtime;

namespace ProceduralUITool.Tests
{

    public class ProceduralUI_tests
    {
        private GameObject _testGameObject;
        private ProceduralUIComponent _effectComponent;
        private ProceduralUIProfile _testProfile;

        [SetUp]
        public void Setup()
        {
            _testGameObject = new GameObject("TestProceduralUI");
            _testGameObject.AddComponent<Image>();
            
            _testProfile = ScriptableObject.CreateInstance<ProceduralUIProfile>();
            _testProfile.name = "TestProfile";
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null) Object.DestroyImmediate(_testGameObject);
            if (_testProfile != null) Object.DestroyImmediate(_testProfile);
        }

        [Test]
        public void ComponentCanBeAdded()
        {
            _effectComponent = _testGameObject.AddComponent<ProceduralUIComponent>();
            Assert.IsNotNull(_effectComponent, "Component should not be null after being added.");
        }

        [Test]
        public void ProfileHasCorrectDefaultValues()
        {
            var profile = ScriptableObject.CreateInstance<ProceduralUIProfile>();
            profile.ResetToDefaults();

            Assert.AreEqual(10f, profile.globalCornerRadius, "Default global corner radius is incorrect.");
            Assert.AreEqual(2f, profile.borderWidth, "Default border width is incorrect.");
            Assert.IsFalse(profile.useIndividualCorners, "useIndividualCorners should be false by default.");
            
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ComponentCanSetProfile()
        {
            _effectComponent = _testGameObject.AddComponent<ProceduralUIComponent>();
            _effectComponent.SetProfile(_testProfile);
            Assert.AreEqual(_testProfile, _effectComponent.profile, "The profile was not set correctly on the component.");
        }
    }
}