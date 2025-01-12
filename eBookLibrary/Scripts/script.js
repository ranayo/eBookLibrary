document.getElementById('feedbackForm').addEventListener('submit', function (event) {
    event.preventDefault(); // Prevent form from refreshing the page

    // Get the feedback field
    const feedbackElement = document.getElementById('feedback');
    if (!feedbackElement) {
        console.error("Feedback input field not found!");
        alert("An error occurred. Please refresh the page and try again.");
        return;
    }

    // Get the rating and feedback values
    const rating = document.getElementById('rating').value;
    const feedback = feedbackElement.value;

    // Check if feedback is empty
    if (!feedback.trim()) {
        alert("Feedback cannot be empty!");
        return;
    }

    // Create a new feedback card
    const feedbackCard = document.createElement('div');
    feedbackCard.className = 'col-md-4';
    feedbackCard.innerHTML = `
        <div class="card shadow-sm mb-4">
            <div class="card-body">
                <h5 class="card-title">Rating: ${rating}/5</h5>
                <p class="card-text">${feedback}</p>
            </div>
        </div>
    `;

    // Add the new feedback card to the feedback container
    document.getElementById('feedbackContainer').appendChild(feedbackCard);

    // Clear the form fields
    document.getElementById('feedbackForm').reset();
});
